param([Parameter(Mandatory=$true)][string]$Source)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Path (Join-Path $project 'analysis') -Force | Out-Null
$source = [IO.Path]::GetFullPath($Source)
if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Branding source image not found.' }
$folder = Join-Path $project 'assets\branding'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$logo = Join-Path $folder 'apk-runner-logo.png'
$icon = Join-Path $folder 'apk-runner.ico'
if ($source -ne [IO.Path]::GetFullPath($logo)) { Copy-Item -LiteralPath $source -Destination $logo }
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
public static class ApkIconPackaging {
    public static void Create(string source, string destination) {
        int[] sizes = {16,24,32,48,64,128,256};
        var frames = new List<byte[]>();
        using (Image original = Image.FromFile(source)) {
            foreach (int size in sizes) {
                using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                using (var stream = new MemoryStream()) {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.DrawImage(original, new Rectangle(0, 0, size, size));
                    bitmap.Save(stream, ImageFormat.Png);
                    frames.Add(stream.ToArray());
                }
            }
        }
        using (var output = new BinaryWriter(File.Create(destination))) {
            output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)sizes.Length);
            uint offset = (uint)(6 + 16 * sizes.Length);
            for (int i=0; i<sizes.Length; i++) {
                output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                output.Write((byte)0); output.Write((byte)0); output.Write((ushort)1); output.Write((ushort)32);
                output.Write((uint)frames[i].Length); output.Write(offset);
                offset += (uint)frames[i].Length;
            }
            foreach (byte[] frame in frames) output.Write(frame);
        }
    }
}
'@
[ApkIconPackaging]::Create($logo, $icon)
$before=(Get-FileHash -LiteralPath $source).Hash
$after=(Get-FileHash -LiteralPath $logo).Hash
if($before -ne $after){throw 'Source artwork content changed during import'}
$record=[pscustomobject]@{Source=$source;Logo=$logo;Icon=$icon;SourceSHA256=$before;LogoSHA256=$after;IconSizes=@(16,24,32,48,64,128,256)}
$record|ConvertTo-Json -Depth 3|Set-Content -LiteralPath (Join-Path $project 'analysis\branding-import.json') -Encoding utf8
$record|ConvertTo-Json -Depth 3
