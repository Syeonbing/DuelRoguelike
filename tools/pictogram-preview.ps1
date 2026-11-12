# 픽토그램 미리보기: Assets/UI/Resources/Pictograms.txt를 PNG 한 장으로 그린다 (큰 그림 + 실제 카드 크기 그림).
# 사용: & tools\pictogram-preview.ps1 -Out <png 경로> [-Names 마법사족,악마족]
param(
    [Parameter(Mandatory = $true)][string]$Out,
    [string[]]$Names
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (Join-Path $root 'Assets/Scripts/UI/PictogramData.cs')
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$text = [IO.File]::ReadAllText((Join-Path $root 'Assets/UI/Resources/Pictograms.txt'))
$all = [DuelRoguelike.UI.PictogramParser]::Parse($text)
$list = @($all.Values)
if ($Names) { $list = @($list | Where-Object { $Names -contains $_.Name }) }

$cols = 8; $cw = 150; $ch = 170
$rows = [Math]::Ceiling($list.Count / $cols)
$visual = New-Object System.Windows.Media.DrawingVisual
$dc = $visual.RenderOpen()
$bg = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromRgb(0x1c, 0x18, 0x14))
$cell = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromRgb(0x2c, 0x25, 0x1e))
$ink = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromRgb(0xf0, 0xe6, 0xd2))
$dc.DrawRectangle($bg, $null, (New-Object System.Windows.Rect 0, 0, ($cols * $cw), ($rows * $ch)))
$face = New-Object System.Windows.Media.Typeface 'Malgun Gothic'

function Draw-Picto($p, [double]$x, [double]$y, [double]$size) {
    $dc.PushTransform((New-Object System.Windows.Media.MatrixTransform ($size / 48), 0, 0, ($size / 48), $x, $y))
    foreach ($s in $p.Shapes) {
        $svg = [DuelRoguelike.UI.Pictogram]::ToSvg($s.Path)
        if ($s.Stroke) {
            $g = [System.Windows.Media.Geometry]::Parse($svg)
            $pen = New-Object System.Windows.Media.Pen $ink, $s.Width
            $pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round'; $pen.LineJoin = 'Round'
            $dc.DrawGeometry($null, $pen, $g)
        } else {
            $g = [System.Windows.Media.Geometry]::Parse('F0 ' + $svg)
            $dc.DrawGeometry($ink, $null, $g)
        }
    }
    $dc.Pop()
}

for ($i = 0; $i -lt $list.Count; $i++) {
    $p = $list[$i]
    $x = ($i % $cols) * $cw; $y = [Math]::Floor($i / $cols) * $ch
    $dc.DrawRectangle($cell, $null, (New-Object System.Windows.Rect ($x + 4), ($y + 4), ($cw - 8), ($ch - 8)))
    Draw-Picto $p ($x + 12) ($y + 14) 96
    Draw-Picto $p ($x + 112) ($y + 40) 30
    $ft = New-Object System.Windows.Media.FormattedText ($p.Name, [Globalization.CultureInfo]::GetCultureInfo('ko-KR'), 'LeftToRight', $face, 15, $ink, 1.0)
    $dc.DrawText($ft, (New-Object System.Windows.Point ($x + 12), ($y + 126)))
}
$dc.Close()
$bmp = New-Object System.Windows.Media.Imaging.RenderTargetBitmap ($cols * $cw), ($rows * $ch), 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
$bmp.Render($visual)
$enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
$fs = [IO.File]::Create($Out); $enc.Save($fs); $fs.Close()
"$($list.Count)개를 그렸습니다: $Out"
