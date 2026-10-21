param([string]$AssemblyPath)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
if (!$AssemblyPath) { $AssemblyPath = Join-Path $project 'artifacts\updater-app\APKRunner.exe' }
Add-Type -AssemblyName System.Drawing
$icoPath = Join-Path $project 'assets\branding\apk-runner.ico'
$reader = [IO.BinaryReader]::new([IO.File]::OpenRead($icoPath))
try {
    if ($reader.ReadUInt16() -ne 0 -or $reader.ReadUInt16() -ne 1) { throw 'Invalid Windows icon header.' }
    $count = $reader.ReadUInt16()
    if ($count -ne 7) { throw 'Expected seven Windows icon sizes.' }
    $frames = @(for($i=0; $i -lt $count; $i++) {
        $width = [int]$reader.ReadByte(); $height = [int]$reader.ReadByte()
        if (!$width) { $width=256 }; if (!$height) { $height=256 }
        $null=$reader.ReadBytes(6)
        [pscustomobject]@{width=$width;height=$height;length=$reader.ReadUInt32();offset=$reader.ReadUInt32()}
    })
    foreach($frame in $frames) {
        $reader.BaseStream.Position=$frame.offset
        $stream=[IO.MemoryStream]::new($reader.ReadBytes($frame.length))
        $bitmap=[Drawing.Bitmap]::FromStream($stream)
        try {
            if($bitmap.Width -ne $frame.width -or $bitmap.Height -ne $frame.height) { throw 'Icon frame dimensions mismatch.' }
            foreach($point in @(@(0,0),@(($bitmap.Width-1),0),@(0,($bitmap.Height-1)),@(($bitmap.Width-1),($bitmap.Height-1)))) {
                if($bitmap.GetPixel($point[0],$point[1]).A -ne 0) { throw ('Opaque icon corner at size '+$frame.width) }
            }
        } finally { $bitmap.Dispose(); $stream.Dispose() }
    }
} finally { $reader.Dispose() }
$assembly=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
foreach($resource in @(@('APKRunner.BrandIcon.ico',$icoPath),@('APKRunner.BrandLogo.png',(Join-Path $project 'assets\branding\apk-runner-logo.png')))) {
    $stream=$assembly.GetManifestResourceStream($resource[0]); $memory=[IO.MemoryStream]::new()
    if(!$stream) { throw 'Missing embedded branding.' }
    try {
        $stream.CopyTo($memory)
        if([Convert]::ToBase64String($memory.ToArray()) -ne [Convert]::ToBase64String([IO.File]::ReadAllBytes($resource[1]))) { throw 'Embedded branding differs from current artwork.' }
    } finally { $stream.Dispose(); $memory.Dispose() }
}
Write-Output 'Verified all seven ICO frames have transparent corners and both embedded branding resources match.'
