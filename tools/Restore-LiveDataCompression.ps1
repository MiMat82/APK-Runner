# Preserve the active app-data write path; a 1% disk saving is not worthwhile here.
param([Parameter(Mandatory=$true)][string]$RuntimeRoot)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$runtime = (Resolve-Path -LiteralPath $RuntimeRoot).Path.TrimEnd('\')
if (@(Get-CimInstance Win32_Process | Where-Object { $_.Name -match '^(APKRunner.*|emulator.*|qemu-system-.*|scrcpy)\.exe$' }).Count) { throw 'Close APK Runner and Android first.' }
$guard = [IO.File]::Open((Join-Path $runtime 'operation.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
try {
    $recordPath = Join-Path $project 'analysis\smaller-compression-applied.json'
    $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
    $manifestPath = Join-Path $project 'analysis\smaller-files-after.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $restored = @()
    foreach ($name in @('APKRunner', 'APKRunnerTV')) {
        $path = Join-Path $runtime ('avd\' + $name + '.avd\userdata-qemu.img.qcow2')
        for ($current = $path; ; $current = [IO.Path]::GetDirectoryName($current)) {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Link refused: $current" }
            if ($current -eq $runtime) { break }
        }
        $entry = $record.Files | Where-Object Path -eq $path
        if (!$entry -or !$entry.ChangedCompression) { continue }
        $before = (Get-FileHash -LiteralPath $path).Hash
        & compact.exe /U /F /Q $path
        if ($LASTEXITCODE -ne 0) { throw 'Unable to restore live data compression policy.' }
        $after = (Get-FileHash -LiteralPath $path).Hash
        if ($before -ne $after) { throw 'Data-content verification failed.' }
        $oldDiskBytes = $entry.AfterDiskBytes
        $entry.ChangedCompression = $false
        $entry.AfterDiskBytes = $entry.BeforeDiskBytes
        ($manifest | Where-Object Path -eq $path).DiskBytes = $entry.BeforeDiskBytes
        $record.AfterDiskBytes += $entry.BeforeDiskBytes - $oldDiskBytes
        $record.FreedBytes = $record.BeforeDiskBytes - $record.AfterDiskBytes
        $restored += [pscustomobject]@{Path=$path;BeforeSHA256=$before;AfterSHA256=$after;Reason='Preserve uncompressed active app-data writes'}
    }
    $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $recordPath -Encoding UTF8
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    $restored | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'analysis\smaller-live-data-policy.json') -Encoding UTF8
    Write-Output 'Both active userdata overlays retain their original uncompressed write path; data hashes match.'
} finally { $guard.Dispose() }
