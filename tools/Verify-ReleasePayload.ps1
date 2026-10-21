param([Parameter(Mandatory=$true)][string]$ReleaseDirectory,
    [Parameter(Mandatory=$true)][string]$Version,
    [string]$ReportPath)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid stable release version.' }
$project = Split-Path $PSScriptRoot -Parent
$directory = [IO.Path]::GetFullPath($ReleaseDirectory)
if ((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Release directory cannot be a link.' }
$packageName = 'APKRunner-' + $Version + '-full.nupkg'
$expected = @($packageName,'APKRunner-win-Portable.zip','APKRunner-win-Setup.exe','assets.win.json','RELEASES','releases.win.json','SHA256SUMS.txt')
$items = @(Get-ChildItem -LiteralPath $directory -Force)
if ($items.Count -ne $expected.Count) { throw 'Expected exactly seven release assets and no extra files or directories.' }
foreach ($item in $items) {
    if ($item.PSIsContainer -or $item.Name -cnotin $expected -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw ('Unexpected or linked release asset: ' + $item.Name) }
}
$sums = @{}
foreach ($line in Get-Content -LiteralPath (Join-Path $directory 'SHA256SUMS.txt')) {
    if (!$line.Trim()) { continue }
    if ($line -notmatch '^([0-9a-fA-F]{64})  ([^\\/:]+)$') { throw 'Invalid SHA256SUMS entry or unsafe asset path.' }
    $name = $Matches[2]; $hash = $Matches[1]
    if ($name -ceq 'SHA256SUMS.txt' -or $name -cnotin $expected -or $sums.ContainsKey($name)) { throw 'Unexpected or duplicate SHA256SUMS entry.' }
    if ((Get-FileHash -LiteralPath (Join-Path $directory $name) -Algorithm SHA256).Hash -ne $hash) { throw ('Release asset checksum mismatch: ' + $name) }
    $sums[$name] = $hash.ToUpperInvariant()
}
if ($sums.Count -ne 6) { throw 'SHA256SUMS must cover every non-checksum release asset exactly once.' }
$packagePath = Join-Path $directory $packageName
$package = Get-Item -LiteralPath $packagePath
$packageSha1 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA1).Hash
$feed = Get-Content -LiteralPath (Join-Path $directory 'releases.win.json') -Raw | ConvertFrom-Json
$assets = @($feed.Assets)
if ($assets.Count -ne 1) { throw 'Expected a single full package in the stable release feed.' }
$asset = $assets[0]
if ($asset.PackageId -cne 'APKRunner' -or $asset.Version -cne $Version -or $asset.Type -cne 'Full' -or $asset.FileName -cne $packageName -or [long]$asset.Size -ne $package.Length -or $asset.SHA256 -ne $sums[$packageName] -or $asset.SHA1 -ne $packageSha1) { throw 'Stable release feed version, package identity, size or hashes do not match the package.' }
$legacy = @((Get-Content -LiteralPath (Join-Path $directory 'RELEASES')) | Where-Object {$_.Trim()})
if ($legacy.Count -ne 1 -or $legacy[0] -cne ($packageSha1 + ' ' + $packageName + ' ' + $package.Length)) { throw 'Legacy RELEASES feed does not match the full update package.' }
$index = Get-Content -LiteralPath (Join-Path $directory 'assets.win.json') -Raw | ConvertFrom-Json
$index = @($index)
$indexed = @{}
foreach ($entry in $index) {
    if ($indexed.ContainsKey($entry.Type)) { throw 'Duplicate release asset index type.' }
    $indexed[$entry.Type] = $entry.RelativeFileName
}
if ($index.Count -ne 3 -or $indexed['Installer'] -cne 'APKRunner-win-Setup.exe' -or $indexed['Full'] -cne $packageName -or $indexed['Portable'] -cne 'APKRunner-win-Portable.zip') { throw 'Release asset index does not describe exactly the expected installer, full package and portable archive.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$allowedApp = @('APKRunner.exe','APKRunner.exe.config','LICENSE.txt','THIRD-PARTY-NOTICES.md','Newtonsoft.Json-LICENSE.txt','Newtonsoft.Json.dll','sq.version','Velopack-LICENSE.txt','Velopack.dll')
$allowedPackage = @('[Content_Types].xml','APKRunner.nuspec','setup.ico','_rels/.rels','lib/app/APK Runner_ExecutionStub.exe','lib/app/Squirrel.exe') + @($allowedApp | ForEach-Object {'lib/app/' + $_})
$allowedPortable = @('.portable','APK Runner.exe','Update.exe') + @($allowedApp | ForEach-Object {'current/' + $_})
function Read-VerifiedArchive([string]$Path, [string[]]$Allowed) {
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    $contents = @{}
    try {
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName
            if ($name -match '(^[/\\]|[\\:]|(^|/)\.\.?(/|$))' -or $contents.ContainsKey($name)) { throw ('Unsafe or duplicate archive path: ' + $name) }
            if ($name -cnotin $Allowed) { throw ('Unexpected archive entry; runtime, saved data and secrets must never ship: ' + $name) }
            if ($entry.Length -gt 32MB) { throw ('Unexpectedly large launcher archive entry: ' + $name) }
            $inputStream = $entry.Open(); $memory = [IO.MemoryStream]::new()
            try { $inputStream.CopyTo($memory); $contents[$name] = $memory.ToArray() }
            finally { $inputStream.Dispose(); $memory.Dispose() }
        }
    } finally { $archive.Dispose() }
    if ($contents.Count -ne $Allowed.Count) { throw 'Missing required launcher archive entries.' }
    return ,$contents
}
$packed = Read-VerifiedArchive $packagePath $allowedPackage
$portable = Read-VerifiedArchive (Join-Path $directory 'APKRunner-win-Portable.zip') $allowedPortable
foreach ($name in $allowedApp) {
    if ([Convert]::ToBase64String($packed['lib/app/' + $name]) -cne [Convert]::ToBase64String($portable['current/' + $name])) { throw ('Portable and update package payload differ: ' + $name) }
}
foreach ($pair in @(@('lib/app/APK Runner_ExecutionStub.exe','APK Runner.exe'),@('lib/app/Squirrel.exe','Update.exe'))) {
    if ([Convert]::ToBase64String($packed[$pair[0]]) -cne [Convert]::ToBase64String($portable[$pair[1]])) { throw 'Portable launcher/updater does not match the full package.' }
}
function Assert-Manifest([byte[]]$Bytes) {
    $document = [Xml.XmlDocument]::new(); $document.XmlResolver = $null
    $settings = [Xml.XmlReaderSettings]::new(); $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit; $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create([IO.StringReader]::new([Text.Encoding]::UTF8.GetString($Bytes)), $settings)
    try { $document.Load($reader) } finally { $reader.Dispose() }
    $metadata = $document.SelectSingleNode('/*[local-name()="package"]/*[local-name()="metadata"]')
    foreach ($pair in @(@('id','APKRunner'),@('version',$Version),@('mainExe','APKRunner.exe'),@('channel','win'),@('rid','win-x64'))) {
        $node = $metadata.SelectSingleNode('*[local-name()="' + $pair[0] + '"]')
        if (!$node -or $node.InnerText -cne $pair[1]) { throw ('Package manifest mismatch: ' + $pair[0]) }
    }
}
Assert-Manifest $packed['APKRunner.nuspec']
Assert-Manifest $packed['lib/app/sq.version']
$executableBytes = $packed['lib/app/APKRunner.exe']
$inspection = Join-Path $project ('analysis\release-payload-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $inspection | Out-Null
$executablePath = Join-Path $inspection 'APKRunner.exe'
[IO.File]::WriteAllBytes($executablePath, $executableBytes)
# AssemblyName and FileVersionInfo inspect metadata without launching/loading app code.
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($executablePath).Version.ToString(3)
$productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($executablePath).ProductVersion
if ($assemblyVersion -cne $Version -or $productVersion -cnotmatch ('^' + [regex]::Escape($Version) + '(\+[0-9A-Za-z.-]+)?$')) { throw 'Packaged application version differs from the release version.' }
$unicode0 = [Text.Encoding]::Unicode.GetString($executableBytes)
$unicode1 = [Text.Encoding]::Unicode.GetString($executableBytes, 1, $executableBytes.Length - 1)
foreach ($text in @('Check runtime','I accept the Android SDK terms','Check for updates','Storage usage','Landscape','Portrait','en-US')) {
    if (!$unicode0.Contains($text) -and !$unicode1.Contains($text)) { throw ('Expected English application text missing: ' + $text) }
}
if (!$ReportPath) { $ReportPath = Join-Path $inspection 'verified-payload.json' }
$report = [ordered]@{version=$Version;assets=$expected;asset_hashes=$sums;package_bytes=$package.Length;package_entries=$packed.Count;portable_entries=$portable.Count;assembly_version=$assemblyVersion;product_version=$productVersion;required_license_and_notices_present=$true;english_text_present=$true;runtime_or_userdata_bundled=$false;application_executed=$false;verified_utc=[DateTime]::UtcNow.ToString('o')}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
Write-Output ('Verified seven release assets, matching update feeds, English '+$Version+' payload and no bundled Android/runtime data. Report: '+$ReportPath)
