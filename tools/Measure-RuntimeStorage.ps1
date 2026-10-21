param([Parameter(Mandatory=$true)][string]$RuntimeRoot)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$runtime = (Resolve-Path -LiteralPath $RuntimeRoot).Path.TrimEnd('\')
$app = Split-Path $runtime -Parent
New-Item -ItemType Directory -Path (Join-Path $project 'analysis') -Force | Out-Null
Add-Type -Path (Join-Path $project 'src\preview\NativeStorage.cs')
$rows = @(Get-ChildItem -LiteralPath $runtime -File -Force -Recurse | ForEach-Object { [pscustomobject]@{
    RelativePath=$_.FullName.Substring($runtime.Length+1);Bytes=$_.Length;DiskBytes=[APKRunner.NativeStorage]::DiskBytes($_.FullName);Attributes=[string]$_.Attributes
} })
$rows | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'analysis\smaller-final-inventory.json') -Encoding UTF8
$top = @(Get-ChildItem -LiteralPath $app -File -Force)
$result = [ordered]@{
    LogicalRuntimeBytes=($rows | Measure-Object Bytes -Sum).Sum
    OccupiedRuntimeBytes=($rows | Measure-Object DiskBytes -Sum).Sum
    OccupiedAppFolderBytes=(($rows | Measure-Object DiskBytes -Sum).Sum + ($top | ForEach-Object { [APKRunner.NativeStorage]::DiskBytes($_.FullName) } | Measure-Object -Sum).Sum)
    RuntimeFiles=$rows.Count
    Components=@($rows | Group-Object { $_.RelativePath.Split('\')[0] } | ForEach-Object { [pscustomobject]@{
        Name=$_.Name;LogicalBytes=($_.Group | Measure-Object Bytes -Sum).Sum;DiskBytes=($_.Group | Measure-Object DiskBytes -Sum).Sum;Files=$_.Count
    } })
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $project 'analysis\smaller-final-storage.json') -Encoding UTF8
[pscustomobject]$result | Select-Object LogicalRuntimeBytes,OccupiedRuntimeBytes,OccupiedAppFolderBytes,RuntimeFiles | ConvertTo-Json
