param([Parameter(Mandatory=$true)][string]$RuntimeRoot, [switch]$Disable)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$runtime = (Resolve-Path -LiteralPath $RuntimeRoot).Path
$marker = Join-Path $runtime 'fast-rendering.enabled'
if ($Disable) {
    if (Test-Path -LiteralPath $marker) { Move-Item -LiteralPath $marker -Destination ($marker + '.disabled-' + [guid]::NewGuid().ToString('N')) }
    Write-Output 'Next phone boot uses automatic graphics and H.264. Existing app data is unchanged.'
} else {
    [IO.File]::WriteAllText($marker, [Environment]::MachineName, (New-Object Text.UTF8Encoding($false)))
    Write-Output 'Automatic graphics, supported VP8 video and cold-boot readiness enabled on this computer. Existing app data is unchanged.'
}
