param([string]$Version, [string]$OutputDirectory, [string]$SigningConfiguration, [switch]$AllowUnsignedDevelopment)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Path (Join-Path $project 'analysis') -Force | Out-Null
if (!$Version) { $Version = (Get-Content -LiteralPath (Join-Path $project 'release\VERSION.txt') -Raw).Trim() }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a stable major.minor.patch release version.' }
if (!$SigningConfiguration) { $SigningConfiguration = Join-Path $project 'release\signing.local.json' }
. (Join-Path $PSScriptRoot 'Signing.ps1')
$signing = $null
if (!$AllowUnsignedDevelopment) { $signing = Get-ApkSigningArguments -ConfigurationPath $SigningConfiguration }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $project ('artifacts\release-' + $Version + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
# Package a fresh build directory so stale local outputs cannot enter a release.
$app = Join-Path $project ('artifacts\release-app-' + [guid]::NewGuid().ToString('N'))
& (Join-Path $project 'Build-App.ps1') -Version $Version -OutputPath (Join-Path $app 'APKRunner.exe')
if ($LASTEXITCODE -ne 0) { throw 'Application build failed' }
Push-Location $project
try {
    $vpk = Join-Path $project '.tools\velopack\vpk.exe'
    if (!(Test-Path -LiteralPath $vpk)) {
        dotnet tool install vpk --version 1.2.161 --tool-path (Split-Path $vpk -Parent) --configfile (Join-Path $project 'NuGet.Config')
        if ($LASTEXITCODE -ne 0) { throw 'Updater tool installation failed' }
    }
    $packArguments = @('pack','--packId','APKRunner','--packVersion',$Version,'--packDir',$app,'--mainExe','APKRunner.exe','--packTitle','APK Runner','--icon',(Join-Path $project 'assets\branding\apk-runner.ico'),'--framework','net48','--runtime','win-x64','--outputDir',$OutputDirectory,'--channel','win','--releaseNotes',(Join-Path $project 'release\NOTES.md'),'--delta','None')
    if ($signing) { $packArguments += $signing.Arguments }
    & $vpk @packArguments
    if ($LASTEXITCODE -ne 0) { throw 'Release packaging failed' }
} finally { Pop-Location }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$package = Join-Path $OutputDirectory ('APKRunner-' + $Version + '-full.nupkg')
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $forbidden = @($archive.Entries | Where-Object { $_.FullName -match '(?i)(^|/)(runtime|avd|android-sdk)(/|$)|userdata|encryptionkey|\.pdb$' })
    if ($forbidden.Count) { throw 'Release package unexpectedly contains data, runtime or debug files.' }
} finally { $archive.Dispose() }
if (!$AllowUnsignedDevelopment) {
    & (Join-Path $PSScriptRoot 'Verify-SignedRelease.ps1') -ReleaseDirectory $OutputDirectory -Version $Version -ConfigurationPath $SigningConfiguration
} else { Write-Warning 'Unsigned package. Publication requires an explicit unsigned-release authorization and disclosure.' }
Get-ChildItem -LiteralPath $OutputDirectory -File | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name } | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ASCII
$OutputDirectory | Set-Content -LiteralPath (Join-Path $project 'analysis\updater-release-path.txt') -Encoding UTF8
Write-Output ('Release ready: ' + $OutputDirectory)
