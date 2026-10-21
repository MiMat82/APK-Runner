[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$AppRoot,
    [switch]$Apply,
    [string]$ReportPath
)
$ErrorActionPreference = 'Stop'
if (!('APKRunner.NativeStorage' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot '..\src\preview\NativeStorage.cs')
}
$app = (Resolve-Path -LiteralPath $AppRoot).Path.TrimEnd('\')
$runtime = Join-Path $app 'runtime'
if (!(Test-Path -LiteralPath (Join-Path $app 'APKRunner V12.9.exe') -PathType Leaf)) {
    throw 'Expected APKRunner V12.9.exe next to this runtime. Refusing an unknown target.'
}
if (!(Test-Path -LiteralPath $runtime -PathType Container)) { throw 'Runtime folder is missing.' }

function Assert-LocalPath([string]$Target) {
    $absolute = [IO.Path]::GetFullPath($Target).TrimEnd('\')
    if (!$absolute.Equals($app, [StringComparison]::OrdinalIgnoreCase) -and
        !$absolute.StartsWith($app + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Target is outside the app folder: $absolute"
    }
    $current = $absolute
    while ($true) {
        if (Test-Path -LiteralPath $current) {
            $entry = Get-Item -LiteralPath $current -Force
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Link/junction refused: $current" }
        }
        if ($current.Equals($app, [StringComparison]::OrdinalIgnoreCase)) { break }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}

function Get-CheckedFiles([string]$Directory) {
    Assert-LocalPath $Directory
    if (!(Test-Path -LiteralPath $Directory)) { return }
    foreach ($entry in Get-ChildItem -LiteralPath $Directory -Force) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Link/junction refused: $($entry.FullName)" }
        if ($entry.PSIsContainer) { Get-CheckedFiles $entry.FullName }
        else { $entry }
    }
}

Assert-LocalPath $runtime
# Keep the scope exact: no userdata, current images, TV runtime, or arbitrary
# backup folders created by other applications can enter this plan.
$relativeTargets = @(
    'backups\AVD-before-googleapis-20260910182744',
    'avd\APKRunner.avd\snapshots',
    'avd\APKRunnerTV.avd\snapshots',
    'android-sdk\system-images\android-35\default\x86_64'
)
$avdRoot = Join-Path $runtime 'avd'
$configs = @(Get-CheckedFiles $avdRoot | Where-Object Name -EQ 'config.ini')
foreach ($config in $configs) {
    foreach ($line in Get-Content -LiteralPath $config.FullName) {
        if ($line -match '^\s*image\.sysdir\.1\s*=\s*(.+?)\s*$') {
            $image = $Matches[1].Replace('/', '\').TrimEnd('\')
            if ($image -ieq 'system-images\android-35\default\x86_64') {
                throw "The retired image is referenced by an active AVD: $($config.FullName)"
            }
        }
    }
}
$plan = @(foreach ($relative in $relativeTargets) {
    $target = Join-Path $runtime $relative
    Assert-LocalPath $target
    if (Test-Path -LiteralPath $target -PathType Container) {
        $files = @(Get-CheckedFiles $target)
        $bytes = ($files | Measure-Object Length -Sum).Sum
        $diskBytes = ($files | ForEach-Object { [APKRunner.NativeStorage]::DiskBytes($_.FullName) } | Measure-Object -Sum).Sum
        [pscustomobject]@{ RelativePath = $relative; AbsolutePath = $target; Files = $files.Count; Bytes = [long]$bytes; GiB = [math]::Round($bytes / 1GB, 3); DiskBytes = [long]$diskBytes; DiskGiB = [math]::Round($diskBytes / 1GB, 3) }
    }
})
$summary = [pscustomobject]@{
    AppRoot = $app
    Mode = $(if ($Apply) {'Apply'} else {'Preview only; nothing deleted'})
    TotalBytes = [long](($plan | Measure-Object Bytes -Sum).Sum)
    TotalGiB = [math]::Round((($plan | Measure-Object Bytes -Sum).Sum) / 1GB, 3)
    TotalDiskBytes = [long](($plan | Measure-Object DiskBytes -Sum).Sum)
    TotalDiskGiB = [math]::Round((($plan | Measure-Object DiskBytes -Sum).Sum) / 1GB, 3)
    Targets = $plan
    Preserved = @('Phone/ARM64 Google APIs image', 'Android TV/x86 image and device data', 'All active userdata and installed apps')
    Consequences = @('Old rollback copy removed', 'Saved boot sessions discarded; cold boot required', 'Retired default image must be downloaded again if needed')
}
if ($ReportPath) { $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding utf8 }
$summary | ConvertTo-Json -Depth 5
if (!$Apply) { return }

# -Apply is the explicit destructive step. Never stop the user's running apps.
$guard = [IO.File]::Open((Join-Path $runtime 'operation.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
try {
    $processes = @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -match '^(APKRunner.*|emulator.*|qemu-system-.*|scrcpy)\.exe$'
    })
    if ($processes.Count) { throw 'Close APK Runner, Android emulators, and scrcpy before cleanup.' }
    $links = @(Get-CheckedFiles $runtime)
    # Every path was enumerated and checked above; immediately recheck the
    # absolute targets before native PowerShell deletion within this workspace.
    foreach ($item in $plan) { Assert-LocalPath $item.AbsolutePath }
    $marker = Join-Path $runtime 'compact-boot.enabled'
    Assert-LocalPath $marker
    if (Test-Path -LiteralPath $marker) { throw 'Compact boot marker already exists. Preserve it and review the cleanup manually.' }
    # CreateNew avoids following/replacing a concurrent link or existing file.
    $stream = [IO.File]::Open($marker, 'CreateNew', 'Write', 'None')
    try {
        $data = [Text.Encoding]::UTF8.GetBytes("Use -no-snapshot in APKRunner-StoragePreview.exe. Older launchers ignore this preference.`r`n")
        $stream.Write($data, 0, $data.Length)
    } finally { $stream.Dispose() }
    foreach ($item in $plan) {
        Remove-Item -LiteralPath $item.AbsolutePath -Recurse -Force
        if (Test-Path -LiteralPath $item.AbsolutePath) { throw "Deletion did not complete: $($item.AbsolutePath)" }
    }
    Write-Output 'Cleanup completed. Start APKRunner-StoragePreview.exe to keep compact boot enabled.'
} finally { $guard.Dispose() }
