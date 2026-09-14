param([string]$OutPath = "src/StreamDesk.App/Assets/app-icon.ico")

Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

# Background: dark rounded square
$bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 15, 16, 20))
$g.FillRectangle($bgBrush, 0, 0, $size, $size)

# Accent play triangle (Netflix-style red)
$accent = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 229, 9, 20))
$pts = @(
    (New-Object System.Drawing.PointF(96, 64)),
    (New-Object System.Drawing.PointF(96, 192)),
    (New-Object System.Drawing.PointF(200, 128))
)
$g.FillPolygon($accent, $pts)

# Small progress bar under the triangle
$track = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 42, 45, 56))
$g.FillRectangle($track, 64, 208, 128, 10)
$fill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 229, 9, 20))
$g.FillRectangle($fill, 64, 208, 82, 10)

$g.Dispose()

# Save as ICO with PNG-compressed 256px entry (valid modern Windows ICO).
$ms = New-Object System.IO.MemoryStream
$pngStream = New-Object System.IO.MemoryStream
$bmp.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $pngStream.ToArray()

$bw = New-Object System.IO.BinaryWriter($ms)
# ICONDIR
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)
# ICONDIRENTRY (256 -> 0)
$bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0)
$bw.Write([uint16]1); $bw.Write([uint16]32)
$bw.Write([uint32]$pngBytes.Length)
$bw.Write([uint32]22)
$bw.Write($pngBytes)
$bw.Flush()

[System.IO.File]::WriteAllBytes($OutPath, $ms.ToArray())
$bw.Dispose(); $ms.Dispose(); $pngStream.Dispose(); $bmp.Dispose()
Write-Host "Icon written to $OutPath"
