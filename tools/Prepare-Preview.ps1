# Run with Windows PowerShell 5.1 to use the preview's .NET Framework runtime.
param([Parameter(Mandatory=$true)][string]$RuntimeRoot)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$runtimeRoot = (Resolve-Path -LiteralPath $RuntimeRoot).Path
New-Item -ItemType Directory -Path (Join-Path $project 'analysis') -Force | Out-Null
$executable = & (Join-Path $PSScriptRoot 'Get-AppExecutable.ps1')
$assembly = [Reflection.Assembly]::LoadFrom($executable)
$type = $assembly.GetType('APKRunner.Runtime')
$constructor = $type.GetConstructor([type[]]@([string]))
$runtime = $constructor.Invoke([object[]]@([string]$runtimeRoot))
$type.GetMethod('RepairAvdPointers').Invoke($runtime, [object[]]@()) | Out-Null
$state = $type.GetMethod('Check').Invoke($runtime, [object[]]@())
$status = @($state | Select-Object Name,Ready,Detail)
$status | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'analysis\ready-to-launch-status.json') -Encoding UTF8
if (@($status | Where-Object { !$_.Ready }).Count) { throw 'Runtime readiness check failed. See ready-to-launch-status.json.' }
Write-Output ('All ' + $status.Count + ' runtime components are ready. Existing AVD registrations were repaired with original copies retained.')
