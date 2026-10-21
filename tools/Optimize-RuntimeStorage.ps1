[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RuntimeRoot, [switch]$Apply, [switch]$Undo, [switch]$Resume, [switch]$MaintainSnapshots)
$ErrorActionPreference = 'Stop'
if ($Apply -and $Undo) { throw 'Choose Apply or Undo.' }
if ($Resume -and !$Apply) { throw 'Resume requires Apply.' }
if ($MaintainSnapshots -and ($Apply -or $Undo -or $Resume)) { throw 'MaintainSnapshots is a separate operation.' }
$project = Split-Path $PSScriptRoot -Parent
$runtime = (Resolve-Path -LiteralPath $RuntimeRoot).Path.TrimEnd('\')
$app = Split-Path $runtime -Parent
New-Item -ItemType Directory -Path (Join-Path $project 'analysis') -Force | Out-Null
$recordPath = Join-Path $project 'analysis\smaller-compression-applied.json'
if (!('APKRunner.NativeStorage' -as [type])) { Add-Type -Path (Join-Path $project 'src\preview\NativeStorage.cs') }

function Assert-SafePath([string]$Path) {
    $absolute = [IO.Path]::GetFullPath($Path)
    if (!$absolute.StartsWith($runtime + '\', [StringComparison]::OrdinalIgnoreCase) -and $absolute -ne $runtime) { throw "Outside private runtime: $absolute" }
    for ($current = $absolute; ; $current = [IO.Path]::GetDirectoryName($current)) {
        if ((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Link refused: $current" }
        if ($current -eq $app) { break }
    }
}
function Get-RuntimeFiles {
    Assert-SafePath $runtime
    $pending = New-Object 'Collections.Generic.Stack[string]'
    $pending.Push($runtime)
    while ($pending.Count) {
        foreach ($entry in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Link refused: $($entry.FullName)" }
            if ($entry.PSIsContainer) { $pending.Push($entry.FullName) } else { $entry }
        }
    }
}
function Assert-Idle {
    $active = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -match '^(APKRunner.*|emulator.*|qemu-system-.*|scrcpy)\.exe$' })
    if ($active.Count) { throw ('Close APK Runner/Android before compression: ' + (($active | Select-Object -ExpandProperty Name) -join ', ')) }
}
function Invoke-Compression([string]$Path, [bool]$Compress) {
    Assert-SafePath $Path
    $arguments = if ($Compress) { @('/C', '/F', '/Q', $Path) } else { @('/U', '/F', '/Q', $Path) }
    $result = & compact.exe @arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Windows compression failed for $Path : $result" }
}
function Get-SafeHash([string]$Path) {
    if ($Path -eq (Join-Path $runtime 'operation.lock')) {
        $guard.Position = 0
        return (Get-FileHash -InputStream $guard -Algorithm SHA256).Hash
    }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

$files = @(Get-RuntimeFiles | Sort-Object FullName)
$selected = @($files | Where-Object { $_.Length -ge 65536 -and !($_.Attributes -band [IO.FileAttributes]::Compressed) -and $_.Name -ne 'userdata-qemu.img.qcow2' })
if (!$Apply -and !$Undo -and !$MaintainSnapshots) {
    $plan = [ordered]@{ RuntimeRoot=$runtime; Operation='Lossless NTFS compression only; no deletion, repackaging or data-format change';
        CandidateFiles=$selected.Count; CandidateLogicalBytes=($selected | Measure-Object Length -Sum).Sum;
        Files=@($selected | ForEach-Object { [ordered]@{Path=$_.FullName;Bytes=$_.Length;DiskBytes=[APKRunner.NativeStorage]::DiskBytes($_.FullName)} }) }
    $plan | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $project 'analysis\smaller-compression-plan.json') -Encoding UTF8
    [pscustomobject]$plan | Select-Object RuntimeRoot,Operation,CandidateFiles,CandidateLogicalBytes | ConvertTo-Json
    return
}
Assert-Idle
$guard = [IO.File]::Open((Join-Path $runtime 'operation.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
try {
    Assert-Idle
    if ($MaintainSnapshots) {
        $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
        if (!$record.AllFileHashesMatch) { throw 'Complete and verify compression before maintaining snapshot directories.' }
        foreach ($name in @('APKRunner','APKRunnerTV')) {
            $directory = Join-Path $runtime ('avd\' + $name + '.avd\snapshots')
            Assert-SafePath $directory
            if (!(Test-Path -LiteralPath $directory)) { [IO.Directory]::CreateDirectory($directory) | Out-Null }
            if (!((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::Compressed)) {
                Invoke-Compression $directory $true
                $record.InheritanceDirectories += $directory
            }
        }
        $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $recordPath -Encoding UTF8
        Write-Output 'Both snapshot directories will compress newly created boot-memory files; no files were removed.'
        return
    }
    if ($Undo) {
        $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
        foreach ($entry in $record.Files | Where-Object ChangedCompression) {
            $beforeUndo = (Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash
            Invoke-Compression $entry.Path $false
            if ((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -ne $beforeUndo) { throw "Undo content verification failed: $($entry.Path)" }
        }
        foreach ($directory in $record.InheritanceDirectories) { Invoke-Compression $directory $false }
        Write-Output 'Only compression introduced by this pass was removed; files and saved data retained.'
        return
    }
    if ((Test-Path -LiteralPath $recordPath) -and !$Resume) { throw 'This pass already has an apply record; retain it for rollback or use Apply Resume.' }
    if ($Resume) {
        $prior = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
        $manifest = Get-Content -LiteralPath (Join-Path $project 'analysis\smaller-files-before.json') -Raw | ConvertFrom-Json
        if ($prior.AllFileHashesMatch) { throw 'This pass has already completed.' }
        foreach ($entry in $manifest) { Assert-SafePath $entry.Path }
        if ($files.Count -ne $manifest.Count) { throw 'Runtime file count changed; inspect before resuming.' }
        $record = [ordered]@{Files=@($prior.Files);InheritanceDirectories=@($prior.InheritanceDirectories);AllFileHashesMatch=$false;BeforeDiskBytes=$prior.BeforeDiskBytes}
        Write-Output ('Resuming after ' + $record.Files.Count + ' verified files; active ADB dependencies will remain uncompressed.')
    } else {
        $manifest = @()
        Write-Output ('Hashing all ' + $files.Count + ' runtime files before lossless compression.')
        foreach ($file in $files) {
            $manifest += [pscustomobject]@{Path=$file.FullName;Bytes=$file.Length;DiskBytes=[APKRunner.NativeStorage]::DiskBytes($file.FullName);
                SHA256=(Get-SafeHash $file.FullName);Attributes=[string]$file.Attributes}
        }
        $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'analysis\smaller-files-before.json') -Encoding UTF8
        $record = [ordered]@{Files=@();InheritanceDirectories=@();AllFileHashesMatch=$false;BeforeDiskBytes=($manifest | Measure-Object DiskBytes -Sum).Sum}
    }
    $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $recordPath -Encoding UTF8
    foreach ($file in $selected) {
        if (@($record.Files | Where-Object Path -eq $file.FullName).Count) { continue }
        Assert-Idle
        $entry = $manifest | Where-Object Path -eq $file.FullName
        if ((Get-SafeHash $file.FullName) -ne $entry.SHA256) { throw ('File changed since the original manifest: ' + $file.FullName) }
        # ADB can be a shared background service even when no emulator is running.
        $skip = $file.FullName.StartsWith((Join-Path $runtime 'android-sdk\platform-tools\'), [StringComparison]::OrdinalIgnoreCase)
        Write-Output ($(if ($skip) { 'Keeping active ADB dependency: ' } else { 'Compressing ' }) + $file.FullName.Substring($runtime.Length + 1))
        if (!$skip) { Invoke-Compression $file.FullName $true }
        $diskBytes = [APKRunner.NativeStorage]::DiskBytes($file.FullName)
        $changed = ($entry.DiskBytes - $diskBytes) -ge [math]::Max(4096, $entry.DiskBytes * .01)
        if (!$changed -and !$skip) { Invoke-Compression $file.FullName $false; $diskBytes=[APKRunner.NativeStorage]::DiskBytes($file.FullName) }
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        if ($hash -ne $entry.SHA256) { throw ('File content changed: ' + $file.FullName) }
        $record.Files += [ordered]@{Path=$file.FullName;BeforeDiskBytes=$entry.DiskBytes;AfterDiskBytes=$diskBytes;
            BeforeSHA256=$entry.SHA256;AfterSHA256=$hash;ChangedCompression=$changed;SkippedActiveAdbDependency=$skip}
        $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $recordPath -Encoding UTF8
    }
    # Keep future boot-RAM files compressed without applying a blanket policy to userdata directories.
    foreach ($name in @('APKRunner','APKRunnerTV')) {
        $snapshots = Join-Path $runtime ('avd\' + $name + '.avd\snapshots')
        if (Test-Path -LiteralPath $snapshots) {
            foreach ($directory in @((Get-Item -LiteralPath $snapshots)) + @(Get-ChildItem -LiteralPath $snapshots -Directory -Recurse -Force)) {
                if (!($directory.Attributes -band [IO.FileAttributes]::Compressed)) {
                    Invoke-Compression $directory.FullName $true
                    $record.InheritanceDirectories += $directory.FullName
                }
            }
        }
    }
    Write-Output 'Verifying every original runtime file again.'
    $afterFiles = @(Get-RuntimeFiles | Sort-Object FullName)
    if ($afterFiles.Count -ne $manifest.Count) { throw 'Runtime file count changed during compression.' }
    $after = @()
    foreach ($entry in $manifest) {
        $hash = Get-SafeHash $entry.Path
        if ($hash -ne $entry.SHA256) { throw ('Runtime file content changed: ' + $entry.Path) }
        $after += [pscustomobject]@{Path=$entry.Path;Bytes=(Get-Item -LiteralPath $entry.Path).Length;
            DiskBytes=[APKRunner.NativeStorage]::DiskBytes($entry.Path);SHA256=$hash}
    }
    $after | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'analysis\smaller-files-after.json') -Encoding UTF8
    $record.AllFileHashesMatch = $true
    $record['VerifiedFiles'] = $after.Count
    $record['AfterDiskBytes'] = ($after | Measure-Object DiskBytes -Sum).Sum
    $record['FreedBytes'] = $record.BeforeDiskBytes - $record.AfterDiskBytes
    $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $recordPath -Encoding UTF8
    [pscustomobject]$record | Select-Object BeforeDiskBytes,AfterDiskBytes,FreedBytes,VerifiedFiles,AllFileHashesMatch | ConvertTo-Json
} finally { $guard.Dispose() }
