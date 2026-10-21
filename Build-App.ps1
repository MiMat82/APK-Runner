param([string]$OutputPath, [string]$Version)
$ErrorActionPreference = 'Stop'
if (!$Version) { $Version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'release\VERSION.txt') -Raw).Trim() }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a stable major.minor.patch version.' }
if (!$OutputPath) { $OutputPath = Join-Path $PSScriptRoot 'artifacts\updater-app\APKRunner.exe' }
$destination = [IO.Path]::GetFullPath($OutputPath)
$directory = Split-Path $destination -Parent
# Build packages in artifacts; deploy using Setup/updater so active files are not overwritten.
dotnet build (Join-Path $PSScriptRoot 'src\APKRunner.csproj') -c Release -o $directory ('-p:Version=' + $Version) --configfile (Join-Path $PSScriptRoot 'NuGet.Config')
if ($LASTEXITCODE -ne 0) { throw 'APK Runner build failed' }
if ((Split-Path $destination -Leaf) -ne 'APKRunner.exe') { Copy-Item -LiteralPath (Join-Path $directory 'APKRunner.exe') -Destination $destination -Force }
Get-Item -LiteralPath $destination | Select-Object FullName,Length
