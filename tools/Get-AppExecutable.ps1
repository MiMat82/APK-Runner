$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$built = Join-Path $project 'artifacts\updater-app\APKRunner.exe'
if (Test-Path -LiteralPath $built) { $built; return }
$installed = Join-Path $env:LOCALAPPDATA 'APKRunner\current\APKRunner.exe'
if (Test-Path -LiteralPath $installed) { $installed; return }
throw 'Build APK Runner with Build-App.ps1, or install the update-enabled app. Legacy executable paths may be forwarders.'
