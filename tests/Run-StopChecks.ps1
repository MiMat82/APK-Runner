$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $project ('analysis\stop-checks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$executable = Join-Path $fixture 'StopChecks.exe'
$dependencies = Join-Path $project 'artifacts\updater-app'
foreach ($dependency in @('Velopack.dll', 'Newtonsoft.Json.dll')) {
    Copy-Item -LiteralPath (Join-Path $dependencies $dependency) -Destination $fixture
}
& $compiler /nologo /codepage:65001 /target:exe /platform:x64 /optimize+ /main:APKRunner.StopChecks "/out:$executable" /reference:System.Xml.Linq.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.IO.Compression.dll "/reference:$dependencies\Velopack.dll" "/reference:$dependencies\Newtonsoft.Json.dll" "$project\src\preview\*.cs" "/win32icon:$project\assets\branding\apk-runner.ico" "/resource:$project\assets\branding\apk-runner-logo.png,APKRunner.BrandLogo.png" "/resource:$project\assets\branding\apk-runner.ico,APKRunner.BrandIcon.ico" "$PSScriptRoot\StopChecks.cs"
if ($LASTEXITCODE -ne 0) { throw 'Stop check build failed' }
& $executable $fixture
if ($LASTEXITCODE -ne 0) { throw 'Stop checks failed' }
Write-Output ('Stop Android evidence: ' + $fixture)
