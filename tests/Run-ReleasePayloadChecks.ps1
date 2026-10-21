param([string]$ReleaseDirectory)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
if (!$ReleaseDirectory) { $ReleaseDirectory = Join-Path $project 'artifacts\english-public-release-12.11.0' }
$version = '12.11.0'
$packageName = 'APKRunner-' + $version + '-full.nupkg'
$gate = Join-Path $project 'tools\Verify-ReleasePayload.ps1'
$root = Join-Path $project ('analysis\release-payload-checks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$checks = 0
function Fixture([string]$Name) {
    $path = Join-Path $root $Name
    New-Item -ItemType Directory -Path $path | Out-Null
    Get-ChildItem -LiteralPath $ReleaseDirectory -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $path }
    return $path
}
function Rehash([string]$Path) {
    $package = Get-Item -LiteralPath (Join-Path $Path $packageName)
    $sha1 = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA1).Hash
    $sha256 = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash
    $feed = Get-Content -LiteralPath (Join-Path $Path 'releases.win.json') -Raw | ConvertFrom-Json
    $feed.Assets[0].SHA1 = $sha1; $feed.Assets[0].SHA256 = $sha256; $feed.Assets[0].Size = $package.Length
    $feed | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Path 'releases.win.json') -Encoding UTF8
    ($sha1 + ' ' + $packageName + ' ' + $package.Length) | Set-Content -LiteralPath (Join-Path $Path 'RELEASES') -Encoding ASCII
    $lines = @(Get-ChildItem -LiteralPath $Path -File | Where-Object {$_.Name -ne 'SHA256SUMS.txt'} | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name })
    $lines | Set-Content -LiteralPath (Join-Path $Path 'SHA256SUMS.txt') -Encoding ASCII
}
function Add-Entry([string]$Path, [string]$Name) {
    $archive = [IO.Compression.ZipFile]::Open((Join-Path $Path $packageName), [IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = $archive.CreateEntry($Name)
        $writer = [IO.StreamWriter]::new($entry.Open())
        try { $writer.Write('TEST FIXTURE - DO NOT SHIP') } finally { $writer.Dispose() }
    } finally { $archive.Dispose() }
    Rehash $Path
}
function Refuses([string]$Path, [string]$Reason, [string]$Label) {
    $refused = $false
    try { & $gate -ReleaseDirectory $Path -Version $version -ReportPath (Join-Path $root 'negative-report.json') | Out-Null }
    catch {
        if ($_.Exception.Message -notlike ('*' + $Reason + '*')) { throw ('Unexpected rejection for '+$Label+': '+$_.Exception.Message) }
        $refused = $true
    }
    if (!$refused) { throw ('Unsafe release accepted: ' + $Label) }
    $script:checks++; Write-Output ('PASS ' + $Label)
}
& $gate -ReleaseDirectory $ReleaseDirectory -Version $version -ReportPath (Join-Path $root 'verified-original.json') | Out-Null
$checks++; Write-Output 'PASS Actual English release assets, feeds and data-free payload verified without execution'

$path = Fixture 'corrupt-checksum'
[IO.File]::AppendAllText((Join-Path $path 'APKRunner-win-Setup.exe'), 'tampered')
Refuses $path 'checksum mismatch' 'Changed installer bytes are rejected'

$path = Fixture 'wrong-feed-version'
$feed = Get-Content -LiteralPath (Join-Path $path 'releases.win.json') -Raw | ConvertFrom-Json
$feed.Assets[0].Version = '99.0.0'
$feed | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $path 'releases.win.json') -Encoding UTF8
Rehash $path
Refuses $path 'Stable release feed version' 'Wrong feed version is rejected even with valid asset hashes'

$path = Fixture 'traversal'
Add-Entry $path '../userdata.img'
Refuses $path 'Unsafe or duplicate archive path' 'Archive path traversal is rejected after valid feed/hash checks'

$path = Fixture 'android-data'
Add-Entry $path 'lib/app/runtime/avd/userdata-qemu.img'
Refuses $path 'Unexpected archive entry' 'Android runtime and saved-data payloads are rejected'

$path = Fixture 'private-key'
Add-Entry $path 'lib/app/signing.pfx'
Refuses $path 'Unexpected archive entry' 'Private signing key material is rejected'

$path = Fixture 'extra-asset'
[IO.File]::WriteAllText((Join-Path $path 'runtime-path.txt'), 'TEST CONFIGURATION')
Refuses $path 'exactly seven release assets' 'User configuration cannot become an extra uploaded asset'

$path = Fixture 'missing-checksum'
$lines = @(Get-Content -LiteralPath (Join-Path $path 'SHA256SUMS.txt'))
$lines[0..($lines.Count-2)] | Set-Content -LiteralPath (Join-Path $path 'SHA256SUMS.txt') -Encoding ASCII
Refuses $path 'cover every non-checksum' 'Incomplete checksum coverage is rejected'

$path = Fixture 'legacy-feed'
[IO.File]::WriteAllText((Join-Path $path 'RELEASES'), 'WRONG LEGACY FEED')
$lines = @(Get-ChildItem -LiteralPath $path -File | Where-Object {$_.Name -ne 'SHA256SUMS.txt'} | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name })
$lines | Set-Content -LiteralPath (Join-Path $path 'SHA256SUMS.txt') -Encoding ASCII
Refuses $path 'Legacy RELEASES feed' 'Inconsistent legacy updater feed is rejected'

$path = Fixture 'duplicate-archive-entry'
Add-Entry $path 'lib/app/APKRunner.exe'
Refuses $path 'Unsafe or duplicate archive path' 'Duplicate archive destinations are rejected'

$path = Fixture 'wrong-package-version'
$archive = [IO.Compression.ZipFile]::Open((Join-Path $path $packageName), [IO.Compression.ZipArchiveMode]::Update)
try {
    $entry = $archive.GetEntry('APKRunner.nuspec'); $reader = [IO.StreamReader]::new($entry.Open())
    try { $xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $entry.Delete(); $replacement = $archive.CreateEntry('APKRunner.nuspec'); $writer = [IO.StreamWriter]::new($replacement.Open())
    try { $writer.Write($xml.Replace('<version>12.11.0</version>','<version>99.0.0</version>')) } finally { $writer.Dispose() }
} finally { $archive.Dispose() }
Rehash $path
Refuses $path 'Package manifest mismatch: version' 'Wrong internal package version is rejected'

$path = Fixture 'missing-third-party-notices'
$archive = [IO.Compression.ZipFile]::Open((Join-Path $path $packageName), [IO.Compression.ZipArchiveMode]::Update)
try { $archive.GetEntry('lib/app/THIRD-PARTY-NOTICES.md').Delete() } finally { $archive.Dispose() }
Rehash $path
Refuses $path 'Missing required launcher archive entries' 'Project license and third-party notices are required'

[ordered]@{passed=$checks;release=$ReleaseDirectory;fixture_root=$root;executed_application=$false;real_runtime_touched=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'results.json') -Encoding UTF8
Write-Output ('Passed '+$checks+' release payload checks. Evidence: '+$root)
