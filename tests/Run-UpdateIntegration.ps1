$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $project ('analysis\updater-integration-' + [guid]::NewGuid().ToString('N'))
$stage = Join-Path $fixture 'stage'
$feed = Join-Path $fixture 'feed'
$settings = Join-Path $fixture 'settings'
$data = Join-Path $fixture 'existing-android-data'
foreach ($folder in @($fixture,$stage,$feed,$settings,$data)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
$saved = Join-Path $data 'userdata-qemu.img.qcow2'
[IO.File]::WriteAllText($saved, 'PRESERVE INSTALLED APPS AND SAVED DATA')
[IO.File]::WriteAllText((Join-Path $settings 'runtime-path.txt'), $data)
$originalHash = (Get-FileHash -LiteralPath $saved).Hash
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$vpk = Join-Path $project '.tools\velopack\vpk.exe'
$dependencies = Join-Path $project 'artifacts\updater-app'
foreach ($name in @('Velopack.dll','Newtonsoft.Json.dll')) { Copy-Item -LiteralPath (Join-Path $dependencies $name) -Destination $stage }
foreach ($version in @('1.0.0','1.0.1')) {
    $options = @('/nologo','/target:winexe','/platform:x64',"/out:$stage\UpdateProbe.exe",'/reference:System.Web.Extensions.dll',"/reference:$stage\Velopack.dll", "$project\src\preview\RuntimePaths.cs", "$project\src\preview\UpdatePolicy.cs", "$PSScriptRoot\UpdateProbe.cs")
    if ($version -eq '1.0.1') { $options += '/define:NEXT' }
    & $compiler @options
    if ($LASTEXITCODE -ne 0) { throw 'Integration probe compilation failed' }
    & $vpk pack --packId APKRunnerUpdateQA --packVersion $version --packDir $stage --mainExe UpdateProbe.exe --runtime win-x64 --outputDir $feed --channel win --delta None
    if ($LASTEXITCODE -ne 0) { throw 'Integration package creation failed' }
    if ($version -eq '1.0.0') { Copy-Item -LiteralPath (Join-Path $feed 'APKRunnerUpdateQA-win-Portable.zip') -Destination (Join-Path $fixture 'baseline.zip') }
}
$badFeed = Join-Path $fixture 'corrupt-feed'
New-Item -ItemType Directory -Path $badFeed | Out-Null
Copy-Item -LiteralPath (Join-Path $feed 'releases.win.json') -Destination $badFeed
$package = Join-Path $badFeed 'APKRunnerUpdateQA-1.0.1-full.nupkg'
Copy-Item -LiteralPath (Join-Path $feed 'APKRunnerUpdateQA-1.0.1-full.nupkg') -Destination $package
$bytes = [IO.File]::ReadAllBytes($package); $bytes[$bytes.Length - 1] = $bytes[$bytes.Length - 1] -bxor 1; [IO.File]::WriteAllBytes($package, $bytes)
foreach ($scenario in @('corrupt','valid')) {
    $installed = Join-Path $fixture $scenario
    Expand-Archive -LiteralPath (Join-Path $fixture 'baseline.zip') -DestinationPath $installed
    $selectedFeed = if ($scenario -eq 'corrupt') { $badFeed } else { $feed }
    $report = Join-Path $fixture ($scenario + '.json')
    $exe = Join-Path $installed 'current\UpdateProbe.exe'
    $process = Start-Process -FilePath $exe -ArgumentList @(('"'+$selectedFeed+'"'),('"'+$report+'"'),('"'+$settings+'"')) -WindowStyle Hidden -PassThru
    $deadline = (Get-Date).AddSeconds(40)
    while (!(Test-Path -LiteralPath $report) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 250 }
    if (!(Test-Path -LiteralPath $report)) { throw "No integration result: $scenario" }
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if ($result.data_sha256 -ne $originalHash -or (Get-FileHash -LiteralPath $saved).Hash -ne $originalHash) { throw "Saved data changed: $scenario" }
    if ($scenario -eq 'corrupt' -and ($result.result -ne 'corrupt-package-rejected' -or $result.current -ne '1.0.0')) { throw ($result | ConvertTo-Json) }
    if ($scenario -eq 'valid' -and ($result.result -ne 'updated-and-restarted' -or $result.current -ne '1.0.1' -or $result.assembly -ne '1.0.1' -or $result.update_available)) { throw ($result | ConvertTo-Json) }
    Write-Output "PASS $scenario actual-package test; saved data hash unchanged"
}
Write-Output "Update integration evidence: $fixture"
