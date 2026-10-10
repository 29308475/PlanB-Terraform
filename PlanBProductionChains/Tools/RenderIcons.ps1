$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectDirectory = Split-Path $PSScriptRoot -Parent
Add-Type -Path (Join-Path $projectDirectory 'PictogramGeometry.cs')
$assetDirectory = Join-Path $projectDirectory 'Assets\Icons'
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null
$black = [System.Drawing.Color]::Black
$accent = [System.Drawing.ColorTranslator]::FromHtml('#797df5')
$normal = [System.Drawing.ColorTranslator]::FromHtml('#b7b9f5')

function Draw-Icon($graphics, $paths, [float]$x, [float]$y, [float]$size) {
    $scale = $size / 32
    foreach ($pass in @('Outline', 'Accent', 'Filled')) {
        foreach ($definition in $paths) {
            if (($pass -eq 'Filled') -ne $definition.Filled) { continue }
            $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
            $points = [System.Drawing.PointF[]]@($definition.Points | ForEach-Object { [System.Drawing.PointF]::new($x + $_[0] * $scale, $y + $_[1] * $scale) })
            $path.AddLines($points)
            if ($definition.Filled) {
                $path.CloseFigure()
                $fill = if ($definition.AccentFill) { $accent } else { $black }
                $brush = [System.Drawing.SolidBrush]::new($fill)
                $graphics.FillPath($brush, $path); $brush.Dispose()
            } else {
                $tint = if ($pass -eq 'Accent') { $accent } else { $black }
                $width = if ($pass -eq 'Accent') { $definition.Accent } else { $definition.Outline }
                $pen = [System.Drawing.Pen]::new($tint, $width * $scale)
                $pen.StartCap = $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
                $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
                $graphics.DrawPath($pen, $path); $pen.Dispose()
            }
            $path.Dispose()
        }
    }
}

function New-Graphics($bitmap) {
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    return $graphics
}

function Save-Downsampled($bitmap, [int]$width, [int]$height, [string]$file) {
    $output = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = New-Graphics $output
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($bitmap, [System.Drawing.Rectangle]::new(0, 0, $width, $height), 0, 0, $bitmap.Width, $bitmap.Height, [System.Drawing.GraphicsUnit]::Pixel)
    $output.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $output.Dispose()
}

$designs = @(
    @{Name='chain'; Title='PRODUKTIONSKETTEN'; Paths=[PlanBProductionChains.PictogramGeometry]::Chain()},
    @{Name='rebuild'; Title='BAUGRUPPE WIEDERHOLEN'; Paths=[PlanBProductionChains.PictogramGeometry]::Rebuild()}
)
foreach ($design in $designs) {
    [System.IO.File]::WriteAllText((Join-Path $assetDirectory ($design.Name + '.svg')), [PlanBProductionChains.PictogramGeometry]::Svg($design.Paths))
    foreach ($size in @(32, 64, 512)) {
        $bitmap = [System.Drawing.Bitmap]::new($size * 4, $size * 4, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = New-Graphics $bitmap
        Draw-Icon $graphics $design.Paths 0 0 ($size * 4)
        Save-Downsampled $bitmap $size $size (Join-Path $assetDirectory ($design.Name + '-' + $size + '.png'))
        $graphics.Dispose(); $bitmap.Dispose()
    }
}

$sheet = [System.Drawing.Bitmap]::new(3600, 2040, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = New-Graphics $sheet
$g.ScaleTransform(3, 3)
$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#e7e5f2'))
$titleFont = [System.Drawing.Font]::new('Segoe UI', 23, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$labelFont = [System.Drawing.Font]::new('Segoe UI', 15, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$subFont = [System.Drawing.Font]::new('Segoe UI', 17, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$ink = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#303244'))
$g.DrawString('Plan B · Piktogramme', $titleFont, $ink, 32, 23)
$g.DrawString('Vektormaster · gleiche Kontur · originale Spielfarben', $labelFont, $ink, 33, 60)
for ($i = 0; $i -lt 2; $i++) {
    $x = 32 + $i * 584
    $g.FillRectangle([System.Drawing.Brushes]::White, $x, 105, 552, 541)
    $g.DrawString($designs[$i].Title, $subFont, $ink, $x + 24, 126)
    Draw-Icon $g $designs[$i].Paths ($x + 148) 174 256
    $g.DrawString('52 px Button', $labelFont, $ink, $x + 26, 468)
    $g.DrawString('Hover', $labelFont, $ink, $x + 213, 468)
    $g.DrawString('32 px Symbol', $labelFont, $ink, $x + 388, 468)
    foreach ($state in 0..1) {
        $bx = $x + 46 + $state * 186; $by = 506
        $background = if ($state -eq 0) { $normal } else { $accent }
        $brush = [System.Drawing.SolidBrush]::new($background)
        $button = [System.Drawing.Drawing2D.GraphicsPath]::new()
        foreach ($arc in @(@($bx,$by,180), @(($bx+32),$by,270), @(($bx+32),($by+32),0), @($bx,($by+32),90))) {
            $button.AddArc([single]$arc[0], [single]$arc[1], 20, 20, [single]$arc[2], 90)
        }
        $button.CloseFigure(); $g.FillPath($brush, $button)
        Draw-Icon $g $designs[$i].Paths ($bx + 8) ($by + 8) 36
        $button.Dispose(); $brush.Dispose()
    }
    Draw-Icon $g $designs[$i].Paths ($x + 420) 516 32
    $g.DrawString('Schwarz #000000   ·   Blauviolett #797DF5', $labelFont, $ink, $x + 24, 605)
}
Save-Downsampled $sheet 1200 680 (Join-Path $assetDirectory 'pictogram-preview.png')
$g.Dispose(); $sheet.Dispose(); $titleFont.Dispose(); $labelFont.Dispose(); $subFont.Dispose(); $ink.Dispose()
Write-Output "SVG-Master, transparente PNGs und Vorschau erstellt: $assetDirectory"
