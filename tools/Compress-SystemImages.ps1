[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RuntimeRoot, [switch]$Apply, [switch]$Undo)
$ErrorActionPreference = 'Stop'
if ($Apply -and $Undo) { throw 'Choose Apply or Undo.' }
$project = Split-Path $PSScriptRoot -Parent
$runtime = (Resolve-Path -LiteralPath $RuntimeRoot).Path.TrimEnd('\')
$app = Split-Path $runtime -Parent
New-Item -ItemType Directory -Path (Join-Path $project 'analysis') -Force | Out-Null
if (!('APKRunner.NativeStorage' -as [type])) { Add-Type -Path (Join-Path $project 'src\preview\NativeStorage.cs') }
$relativePaths = @(
    'android-sdk\system-images\android-34\android-tv\x86\system.img',
    'android-sdk\system-images\android-35\google_apis\x86_64\system.img',
    'android-sdk\system-images\android-35\default\x86_64\system.img'
)
$files = @($relativePaths | ForEach-Object { Join-Path $runtime $_ } | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
foreach ($file in $files) {
    $current = $file
    while ($true) {
        if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Link refused: $current" }
        if ($current.Equals($app, [StringComparison]::OrdinalIgnoreCase)) { break }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}
if (!$Apply -and !$Undo) {
    $files | ForEach-Object { [pscustomobject]@{Path=$_; LogicalBytes=(Get-Item -LiteralPath $_).Length; DiskBytes=[APKRunner.NativeStorage]::DiskBytes($_)} } | ConvertTo-Json
    return
}
$guard = [IO.File]::Open((Join-Path $runtime 'operation.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
try {
    if (@(Get-CimInstance Win32_Process | Where-Object { $_.Name -match '^(APKRunner.*|emulator.*|qemu-system-.*|scrcpy)\.exe$' }).Count) {
        throw 'Close APK Runner, Android and scrcpy before changing image compression.'
    }
    $records = @()
    $mode = if ($Undo) { 'Undo' } else { 'Apply' }
    foreach ($file in $files) {
        Write-Output ("Hashing and {0}: {1}" -f $mode, $file)
        $before = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        $occupied = [APKRunner.NativeStorage]::DiskBytes($file)
        $watch = [Diagnostics.Stopwatch]::StartNew()
        if ($Undo) { & compact.exe /U /F /Q $file }
        else { & compact.exe /C /F /Q $file }
        if ($LASTEXITCODE -ne 0) { throw "Windows compression failed: $file" }
        $after = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        if ($after -ne $before) { throw "File-content verification failed: $file" }
        $watch.Stop()
        $records += [pscustomobject]@{Path=$file; Mode=$mode; LogicalBytes=(Get-Item -LiteralPath $file).Length;
            BeforeDiskBytes=$occupied; AfterDiskBytes=[APKRunner.NativeStorage]::DiskBytes($file);
            BeforeSHA256=$before; AfterSHA256=$after; Seconds=[math]::Round($watch.Elapsed.TotalSeconds, 2)}
        $records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project ('analysis\compression-' + $mode.ToLowerInvariant() + '.json')) -Encoding utf8
    }
    $records | Select-Object Path,BeforeDiskBytes,AfterDiskBytes,Seconds | Format-Table
} finally { $guard.Dispose() }
