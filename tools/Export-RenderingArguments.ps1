param([Parameter(Mandatory=$true)][string]$RuntimeRoot, [switch]$QueryEncoders, [switch]$WaitForQuietBoot)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$runtimeRoot = (Resolve-Path -LiteralPath $RuntimeRoot).Path
New-Item -ItemType Directory -Path (Join-Path $project 'analysis') -Force | Out-Null
$executable = & (Join-Path $PSScriptRoot 'Get-AppExecutable.ps1')
$assembly = [Reflection.Assembly]::LoadFrom($executable)
$type = $assembly.GetType('APKRunner.Runtime')
$runtime = $type.GetConstructor([type[]]@([string])).Invoke([object[]]@([string]$runtimeRoot))
if ($WaitForQuietBoot) {
    $runtime.Log = [Action[string]]{ param($line) Write-Output $line | Tee-Object -FilePath (Join-Path $project 'analysis\rendering-boot-readiness.txt') -Append | Out-Host }
    $type.GetMethod('WaitForQuietBoot').Invoke($runtime, [object[]]@([string]'emulator-5580')) | Out-Null
}
$report = '--video-codec=vp8' # known image capability; re-query live before benchmarking
if ($QueryEncoders) {
    $scrcpy = Join-Path $runtimeRoot 'scrcpy\scrcpy.exe'
    $result = $type.GetMethod('Run').Invoke($runtime, [object[]]@([string]$scrcpy, [string[]]@('--serial=emulator-5580', '--list-encoders'), [int]20, $null))
    if ($result.Code -ne 0) { throw $result.Text }
    $report = $result.Text
    $report | Set-Content -LiteralPath (Join-Path $project 'analysis\rendering-encoder-capabilities.txt')
}
$output = [ordered]@{
    emulator = $type.GetMethod('EmulatorArguments').Invoke($runtime, [object[]]@($false))
    tv_emulator = $type.GetMethod('EmulatorArguments').Invoke($runtime, [object[]]@($true))
    video = $type.GetMethod('VideoArguments').Invoke($runtime, [object[]]@($false, $report))
    queried_live_encoders = [bool]$QueryEncoders
}
$output | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'analysis\rendering-launch-arguments.json') -Encoding UTF8
$output | ConvertTo-Json -Depth 4
