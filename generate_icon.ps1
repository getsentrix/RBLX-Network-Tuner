Add-Type -AssemblyName System.Drawing

function Create-AppIcon {
    param([string]$outputPath = "app.ico")

    $sizes = @(256, 128, 64, 48, 32, 16)
    $images = @()

    foreach ($sz in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap $sz, $sz, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)

        $pad = [float]($sz * 0.05)
        $w = [float]($sz - 2.0 * $pad)
        $h = [float]($sz - 2.0 * $pad)
        $cx = [float]($sz / 2.0)
        $cy = [float]($sz / 2.0)
        $r = [float]($w / 2.0)

        # Draw outer hexagon badge
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $pts = New-Object "System.Drawing.PointF[]" 6
        for ($i = 0; $i -lt 6; $i++) {
            $angle = [Math]::PI / 3.0 * $i - [Math]::PI / 6.0
            $px = $cx + $r * [Math]::Cos($angle)
            $py = $cy + $r * [Math]::Sin($angle)
            $pts[$i] = New-Object System.Drawing.PointF $px, $py
        }
        $path.AddPolygon($pts)

        # Gradient dark background (#0D131F to #05080E)
        $rect = New-Object System.Drawing.RectangleF $pad, $pad, $w, $h
        $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 13, 19, 31)), ([System.Drawing.Color]::FromArgb(255, 5, 8, 14)), 45.0
        $g.FillPath($brush, $path)
        $brush.Dispose()

        # Neon Cyan / Emerald glowing border
        $borderWidth = [Math]::Max(1.0, [float]($sz * 0.04))
        $penBorder = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(220, 0, 240, 255)), $borderWidth
        $g.DrawPath($penBorder, $path)
        $penBorder.Dispose()

        # Inner dynamic lightning / fiber network pulse
        $boltPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $bpts = New-Object "System.Drawing.PointF[]" 6
        $bpts[0] = New-Object System.Drawing.PointF ($cx + $r * 0.12), ($cy - $r * 0.65)
        $bpts[1] = New-Object System.Drawing.PointF ($cx - $r * 0.45), ($cy + $r * 0.05)
        $bpts[2] = New-Object System.Drawing.PointF ($cx - $r * 0.05), ($cy + $r * 0.05)
        $bpts[3] = New-Object System.Drawing.PointF ($cx - $r * 0.25), ($cy + $r * 0.70)
        $bpts[4] = New-Object System.Drawing.PointF ($cx + $r * 0.45), ($cy - $r * 0.05)
        $bpts[5] = New-Object System.Drawing.PointF ($cx + $r * 0.08), ($cy - $r * 0.05)
        $boltPath.AddPolygon($bpts)

        $boltBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 0, 240, 255)), ([System.Drawing.Color]::FromArgb(255, 0, 255, 163)), 60.0
        $g.FillPath($boltBrush, $boltPath)
        $boltBrush.Dispose()
        $boltPath.Dispose()

        # Center energy core dot
        $dotR = [Math]::Max(2.0, [float]($sz * 0.08))
        $dotRect = New-Object System.Drawing.RectangleF ($cx - $dotR / 2.0), ($cy - $dotR / 2.0), $dotR, $dotR
        $dotBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
        $g.FillEllipse($dotBrush, $dotRect)
        $dotBrush.Dispose()

        $path.Dispose()
        $g.Dispose()

        $images += $bmp
    }

    # Write multi-resolution ICO file
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms

    # ICONDIR
    $bw.Write([uint16]0) # Reserved
    $bw.Write([uint16]1) # Type (ICO)
    $bw.Write([uint16]$images.Count)

    $pngStreams = @()
    foreach ($img in $images) {
        $pms = New-Object System.IO.MemoryStream
        $img.Save($pms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngStreams += $pms
    }

    $offset = 6 + (16 * $images.Count)
    for ($i = 0; $i -lt $images.Count; $i++) {
        $sz = $sizes[$i]
        $wByte = if ($sz -ge 256) { 0 } else { [byte]$sz }
        $hByte = if ($sz -ge 256) { 0 } else { [byte]$sz }

        $bw.Write([byte]$wByte)
        $bw.Write([byte]$hByte)
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]32)
        $bw.Write([uint32]$pngStreams[$i].Length)
        $bw.Write([uint32]$offset)
        $offset += $pngStreams[$i].Length
    }

    for ($i = 0; $i -lt $images.Count; $i++) {
        $data = $pngStreams[$i].ToArray()
        $bw.Write($data, 0, $data.Length)
        $pngStreams[$i].Dispose()
        $images[$i].Dispose()
    }

    [System.IO.File]::WriteAllBytes($outputPath, $ms.ToArray())
    $bw.Dispose()
    $ms.Dispose()

    Write-Host "Generated $outputPath with $($sizes.Count) resolutions."
}

Create-AppIcon -outputPath "app.ico"
