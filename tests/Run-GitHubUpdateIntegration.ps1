param([string]$ExpectedVersion, [switch]$PrepareOnly)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
if (!$ExpectedVersion) { $ExpectedVersion = (Get-Content -LiteralPath (Join-Path $project 'release\VERSION.txt') -Raw).Trim() }
if ($ExpectedVersion -notmatch '^\d+\.\d+\.\d+$' -or [version]$ExpectedVersion -le [version]'12.9.99') { throw 'ExpectedVersion must be a stable version newer than the isolated baseline.' }
$fixture = Join-Path $project ('analysis\github-update-' + [guid]::NewGuid().ToString('N'))
$stage = Join-Path $fixture 'stage'
$feed = Join-Path $fixture 'baseline'
$installed = Join-Path $fixture 'isolated-installation'
$settings = Join-Path $fixture 'settings'
$data = Join-Path $fixture 'existing-android-data'
foreach ($folder in @($stage,$settings,$data)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
$protected = @(
    'avd\APKRunner.avd\userdata-qemu.img.qcow2',
    'avd\APKRunner.avd\encryptionkey.img',
    'avd\APKRunner.avd\config.ini',
    'avd\APKRunnerTV.avd\userdata-qemu.img.qcow2',
    'avd\APKRunnerTV.avd\encryptionkey.img',
    'avd\APKRunnerTV.avd\config.ini',
    'android-sdk\system-images\android-35\google_apis\x86_64\system.img',
    'android-sdk\system-images\android-34\android-tv\x86\system.img'
)
$hashes = @{}
foreach ($relative in $protected) {
    $path = Join-Path $data $relative
    New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force | Out-Null
    [IO.File]::WriteAllText($path, ('PRESERVE EXISTING APPS, SAVES AND COMPATIBILITY: ' + $relative))
    $hashes[$relative] = (Get-FileHash -LiteralPath $path).Hash
}
$registration = Join-Path $settings 'runtime-path.txt'
[IO.File]::WriteAllText($registration, $data)
$registrationHash = (Get-FileHash -LiteralPath $registration).Hash

# Compile the production resolver with only its settings location substituted.
# Exact matching fails safely if the declaration changes; never fall back to a
# real user profile when constructing an integration fixture.
$runtimeSource = Get-Content -LiteralPath (Join-Path $project 'src\preview\RuntimePaths.cs') -Raw
$declaration = 'public static readonly string SettingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "APKRunnerData");'
if ($runtimeSource.Split(@($declaration), [StringSplitOptions]::None).Count -ne 2) { throw 'RuntimePaths settings declaration changed; inspect the isolated test before running.' }
$runtimeSource = $runtimeSource.Replace($declaration, 'public static readonly string SettingsDirectory = Environment.GetEnvironmentVariable("APK_RUNNER_INTEGRATION_SETTINGS");')
$isolatedResolver = Join-Path $fixture 'RuntimePaths.Isolated.cs'
[IO.File]::WriteAllText($isolatedResolver, $runtimeSource)
$dependencies = Join-Path $project 'artifacts\updater-app'
foreach ($name in @('Velopack.dll','Newtonsoft.Json.dll')) { Copy-Item -LiteralPath (Join-Path $dependencies $name) -Destination $stage }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 "/out:$stage\APKRunner.exe" /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.Management.dll "/reference:$stage\Velopack.dll" $isolatedResolver "$project\src\preview\UpdatePolicy.cs" "$project\src\preview\NativeFileLocation.cs" "$project\src\preview\AppUpdates.cs" "$PSScriptRoot\GitHubUpdateProbe.cs"
if ($LASTEXITCODE -ne 0) { throw 'GitHub integration probe build failed' }
& "$project\.tools\velopack\vpk.exe" pack --packId APKRunner --packVersion 12.9.99 --packDir $stage --mainExe APKRunner.exe --runtime win-x64 --outputDir $feed --channel win --delta None
if ($LASTEXITCODE -ne 0) { throw 'Isolated baseline packaging failed' }
Expand-Archive -LiteralPath (Join-Path $feed 'APKRunner-win-Portable.zip') -DestinationPath $installed
if ($PrepareOnly) {
    Write-Output ('Prepared isolated test fixture only; no GitHub update requested: ' + $fixture)
    return
}

$report = Join-Path $fixture 'public-update-result.json'
$previousSettings = $env:APK_RUNNER_INTEGRATION_SETTINGS
try {
    $env:APK_RUNNER_INTEGRATION_SETTINGS = $settings
    Start-Process -FilePath (Join-Path $installed 'current\APKRunner.exe') -ArgumentList @(('"'+$report+'"'),$ExpectedVersion) -WindowStyle Hidden
} finally { $env:APK_RUNNER_INTEGRATION_SETTINGS = $previousSettings }
$deadline = (Get-Date).AddSeconds(90)
do {
    if (Test-Path -LiteralPath $report) { throw (Get-Content -LiteralPath $report -Raw) }
    $currentExe = Join-Path $installed 'current\APKRunner.exe'
    $version = ''; $productVersion = ''; $manifestVersion = ''
    try {
        if (Test-Path -LiteralPath $currentExe) {
            $productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($currentExe).ProductVersion
            $version = $productVersion.Split('+')[0]
        }
        $manifestPath = Join-Path $installed 'current\sq.version'
        if (Test-Path -LiteralPath $manifestPath) { [xml]$currentManifest = Get-Content -LiteralPath $manifestPath -Raw; $manifestVersion = $currentManifest.package.metadata.version }
    } catch { } # The updater may briefly replace either file during this bounded poll.
    if ($version -eq $ExpectedVersion -and $manifestVersion -eq $ExpectedVersion) { break }
    Start-Sleep -Milliseconds 250
} while ((Get-Date) -lt $deadline)
if ($version -ne $ExpectedVersion -or $manifestVersion -ne $ExpectedVersion) { throw 'Public GitHub update test timed out; inspect its isolated installation.' }
$before = Get-Content -LiteralPath ($report+'.before.json') -Raw | ConvertFrom-Json
if ($before.from -ne '12.9.99' -or $before.target -ne $ExpectedVersion -or !$before.required -or !$before.downloaded_and_verified -or $before.runtime -ne $data -or $before.settings -ne $settings) { throw 'GitHub update did not use the isolated version, policy and data paths.' }
[xml]$manifest = Get-Content -LiteralPath (Join-Path $installed 'current\sq.version') -Raw
if ($manifest.package.metadata.version -ne $ExpectedVersion -or $manifest.package.metadata.mainExe -ne 'APKRunner.exe') { throw 'Updated package manifest mismatch.' }

# Inspect the actual new production resolver without starting its GUI or accessing
# its static SettingsDirectory. Resolve receives fixture settings explicitly.
$assembly = [Reflection.Assembly]::LoadFile($currentExe)
if ($assembly.GetName().Version.ToString(3) -ne $ExpectedVersion) { throw 'Updated application assembly version mismatch.' }
$resolved = $assembly.GetType('APKRunner.RuntimePaths').GetMethod('Resolve').Invoke($null, [object[]]@([string](Join-Path $installed 'current'),[string]$settings,$true))
if ($resolved -ne $data) { throw 'Updated production resolver failed to retain the existing external runtime.' }
foreach ($relative in $protected) {
    if ((Get-FileHash -LiteralPath (Join-Path $data $relative)).Hash -ne $hashes[$relative]) { throw ('Fixture data changed: ' + $relative) }
}
if ((Get-FileHash -LiteralPath $registration).Hash -ne $registrationHash) { throw 'Runtime registration changed.' }
$result = [ordered]@{
    repository = $before.repository; from = $before.from; version = $version
    product_version = $productVersion; source_revision = $(if ($productVersion.Contains('+')) { $productVersion.Substring($productVersion.IndexOf('+')+1) } else { $null })
    required = $before.required; downloaded_and_verified = $true; applied = $true
    restart_exercised = $false; installed = $true; runtime = $resolved; settings = $settings
    protected_files_unchanged = $protected.Count; registration_unchanged = $true
    actual_production_runtime_resolver_verified = $true
}
$result | ConvertTo-Json | Set-Content -LiteralPath $report -Encoding UTF8
$fixture | Set-Content -LiteralPath (Join-Path $project 'analysis\github-update-proof-path.txt') -Encoding UTF8
Write-Output ('PASS Public GitHub '+$ExpectedVersion+' detected, required, downloaded, verified and applied; '+$protected.Count+' fixture data files unchanged.')
Write-Output 'The production GUI restart was intentionally not exercised; no real Android runtime or user settings were used.'
Write-Output ('GitHub update evidence: ' + $fixture)
