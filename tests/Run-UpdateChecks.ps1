$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $project ('analysis\update-checks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /reference:System.Drawing.dll "/out:$fixture\UpdateChecks.exe" "$project\src\preview\RuntimePaths.cs" "$project\src\preview\UpdatePolicy.cs" "$project\src\preview\WindowSizing.cs" "$project\src\preview\NativeFileLocation.cs" "$PSScriptRoot\UpdateChecks.cs"
if ($LASTEXITCODE -ne 0) { throw 'Update test compilation failed' }
& "$fixture\UpdateChecks.exe" $fixture
if ($LASTEXITCODE -ne 0) { throw 'Update checks failed' }
