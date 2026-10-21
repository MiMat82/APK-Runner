param([string]$AssemblyPath)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
if (!$AssemblyPath) { $AssemblyPath = & (Join-Path $PSScriptRoot 'Get-AppExecutable.ps1') }
$backup = Join-Path $project ('analysis\shell-icon-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
$desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'APK Runner.lnk'
$start = Join-Path ([Environment]::GetFolderPath('Programs')) 'APK Runner.lnk'
if (Test-Path -LiteralPath $desktop) { Copy-Item -LiteralPath $desktop -Destination (Join-Path $backup 'Desktop-APK Runner.lnk') }
if (Test-Path -LiteralPath $start) { Copy-Item -LiteralPath $start -Destination (Join-Path $backup 'StartMenu-APK Runner.lnk') }
$assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
$settings = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'APKRunnerData'
$launcher = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'APKRunner\APK Runner.exe'
if (!(Test-Path -LiteralPath $launcher -PathType Leaf)) { throw 'Install APK Runner before repairing its shortcuts.' }
$method = $assembly.GetType('APKRunner.ShellBranding').GetMethod('RepairShortcuts')
$icon = $method.Invoke($null, [object[]]@([string]$launcher,[string]$settings))
$shell = New-Object -ComObject WScript.Shell
$results = @(foreach ($path in @($desktop,$start)) {
    $link = $shell.CreateShortcut($path)
    if ($link.IconLocation -ne ($icon+',0') -or !(Test-Path -LiteralPath $link.TargetPath)) { throw 'Shortcut icon/launcher readback failed' }
    [pscustomobject]@{shortcut=$path;target=$link.TargetPath;icon=$link.IconLocation;working_directory=$link.WorkingDirectory}
})
[ordered]@{persistent_icon=$icon;icon_sha256=(Get-FileHash -LiteralPath $icon).Hash;backup=$backup;shortcuts=$results} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'analysis\publisher-shell-icon-proof.json') -Encoding UTF8
Write-Output ('Repaired Desktop and Start Menu icons using '+$icon)
