$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $project ('analysis\github-update-' + [guid]::NewGuid().ToString('N'))
$stage = Join-Path $fixture 'stage'
$feed = Join-Path $fixture 'baseline'
$installed = Join-Path $fixture 'isolated-installation'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$dependencies = Join-Path $project 'artifacts\updater-app'
foreach ($name in @('Velopack.dll','Newtonsoft.Json.dll')) { Copy-Item -LiteralPath (Join-Path $dependencies $name) -Destination $stage }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 "/out:$stage\APKRunner.exe" /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.Management.dll "/reference:$stage\Velopack.dll" "$project\src\preview\RuntimePaths.cs" "$project\src\preview\UpdatePolicy.cs" "$project\src\preview\NativeFileLocation.cs" "$project\src\preview\AppUpdates.cs" "$PSScriptRoot\GitHubUpdateProbe.cs"
if ($LASTEXITCODE -ne 0) { throw 'GitHub integration probe build failed' }
& "$project\.tools\velopack\vpk.exe" pack --packId APKRunner --packVersion 12.9.99 --packDir $stage --mainExe APKRunner.exe --runtime win-x64 --outputDir $feed --channel win --delta None
if ($LASTEXITCODE -ne 0) { throw 'Isolated baseline packaging failed' }
Expand-Archive -LiteralPath (Join-Path $feed 'APKRunner-win-Portable.zip') -DestinationPath $installed
$report = Join-Path $fixture 'public-update-result.json'
Start-Process -FilePath (Join-Path $installed 'current\APKRunner.exe') -ArgumentList ('"'+$report+'"') -WindowStyle Hidden
$deadline = (Get-Date).AddSeconds(50)
while (!(Test-Path -LiteralPath $report) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 250 }
if (!(Test-Path -LiteralPath $report)) { throw 'Public GitHub update test timed out; inspect its isolated installation.' }
$result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if ($result.error -or $result.version -ne '12.10.0' -or !$result.installed -or $result.blocked -or $result.target) { throw ($result | ConvertTo-Json) }
$before = Get-Content -LiteralPath ($report+'.before.json') -Raw | ConvertFrom-Json
if ($before.from -ne '12.9.99' -or $before.target -ne '12.10.0' -or !$before.required -or !$before.downloaded_and_verified -or $before.runtime -ne $result.runtime) { throw 'GitHub update did not preserve expected version and data-path behavior.' }
$fixture | Set-Content -LiteralPath (Join-Path $project 'analysis\github-update-proof-path.txt') -Encoding UTF8
Write-Output 'PASS Public GitHub release detected, required, downloaded, verified, applied and restarted as the actual 12.10.0 application.'
Write-Output ('GitHub update evidence: ' + $fixture)
