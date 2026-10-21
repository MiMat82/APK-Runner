# Preview builds stay separate from the installed updater-managed app.
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\Build-App.ps1" -OutputPath (Join-Path $PSScriptRoot 'artifacts\preview\APKRunner.exe')
