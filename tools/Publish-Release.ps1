param([string]$ReleaseDirectory, [string]$Version, [string]$SigningConfiguration)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
if (!$ReleaseDirectory) { $ReleaseDirectory = (Get-Content -LiteralPath (Join-Path $project 'analysis\updater-release-path.txt') -Raw).Trim() }
if (!$Version) { $Version = (Get-Content -LiteralPath (Join-Path $project 'release\VERSION.txt') -Raw).Trim() }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version' }
if (!$SigningConfiguration) { $SigningConfiguration = Join-Path $project 'release\signing.local.json' }
# Verify the actual extracted binaries immediately before publication; never trust a stale report.
& (Join-Path $PSScriptRoot 'Verify-SignedRelease.ps1') -ReleaseDirectory $ReleaseDirectory -Version $Version -ConfigurationPath $SigningConfiguration
$files = @(Get-ChildItem -LiteralPath $ReleaseDirectory -File | Where-Object { $_.Name -match '\.(exe|zip|nupkg|json)$|^(RELEASES|SHA256SUMS\.txt)$' })
if ($files.Count -ne 7) { throw ('Expected exactly seven release assets; found ' + $files.Count) }
$previousToken = $env:GH_TOKEN
try {
    if (!$previousToken) {
        $env:GIT_TERMINAL_PROMPT='0'; $env:GCM_INTERACTIVE='never'
        $credentials = "protocol=https`nhost=github.com`n`n" | git -c credential.interactive=never credential fill 2>$null
        $password = $credentials | Where-Object { $_ -like 'password=*' } | Select-Object -First 1
        if (!$password) { throw 'Sign GitHub CLI in before publishing. No credentials are stored in this project.' }
        $env:GH_TOKEN = $password.Substring(9)
    }
    & gh release view ('v'+$Version) --repo MiMat82/APK-Runner 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { throw 'This version is already published. Bump the version; do not overwrite an existing release.' }
    $arguments = @('release','create',('v'+$Version),'--repo','MiMat82/APK-Runner','--title',('APK Runner '+$Version),'--notes-file',(Join-Path $project 'release\NOTES.md'),'--latest') + @($files.FullName)
    & gh @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Release publication failed' }
    & gh release view ('v'+$Version) --repo MiMat82/APK-Runner --json url,tagName,isDraft,isPrerelease,assets | Set-Content -LiteralPath (Join-Path $project 'analysis\updater-published-release.json') -Encoding UTF8
    if ($LASTEXITCODE -ne 0) { throw 'Release readback failed' }
} finally { $env:GH_TOKEN = $previousToken }
