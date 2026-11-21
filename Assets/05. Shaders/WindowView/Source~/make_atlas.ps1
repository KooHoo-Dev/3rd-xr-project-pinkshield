param([string]$Src, [string]$Out)
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile($Src)
$bmp = New-Object System.Drawing.Bitmap 512, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAliasGridFit'
$g.Clear([System.Drawing.Color]::FromArgb(255, 40, 30, 50))
$g.DrawImage($img, 0, 0, 256, 256)

function C($r, $gg, $b, $a = 255) { [System.Drawing.Color]::FromArgb($a, $r, $gg, $b) }
function Br($c) { New-Object System.Drawing.SolidBrush $c }
function Poly($ox, $oy, $pts, $c) {
    $arr = @(); for ($k = 0; $k -lt $pts.Count; $k += 2) { $arr += New-Object System.Drawing.PointF ($ox + $pts[$k]), ($oy + $pts[$k + 1]) }
    $g.FillPolygon((Br $c), [System.Drawing.PointF[]]$arr)
}
# 한 점 투시 방 상자: 바깥 120 px, 뒷벽 30~90 px.
function Room($ox, $oy, $ceil, $side, $back, $floor) {
    # 여백 4 px 은 벽색으로 채워 밉맵에서 번지지 않게 한다.
    $g.FillRectangle((Br $side), $ox - 4, $oy - 4, 128, 128)
    Poly $ox $oy @(0,0, 120,0, 90,30, 30,30) $ceil
    Poly $ox $oy @(0,120, 120,120, 90,90, 30,90) $floor
    Poly $ox $oy @(0,0, 30,30, 30,90, 0,120) $side
    Poly $ox $oy @(120,0, 90,30, 90,90, 120,120) ([System.Drawing.Color]::FromArgb(255, [int]($side.R*0.88), [int]($side.G*0.88), [int]($side.B*0.88)))
    $g.FillRectangle((Br $back), $ox + 30, $oy + 30, 60, 60)
    # 천장 등
    $g.FillEllipse((Br (C 255 248 230)), $ox + 52, $oy + 12, 16, 5)
}
function Slime($x, $y, $w, $h, $c) {
    # (x, y) 는 바닥 가운데. 납작한 물방울 몸 + 눈 + 볼 + 반짝임
    $g.FillEllipse((Br (C 0 0 0 50)), $x - $w*0.55, $y - $h*0.12, $w*1.1, $h*0.24)
    $g.FillEllipse((Br $c), $x - $w/2, $y - $h, $w, $h * 1.15)
    $g.FillRectangle((Br $c), $x - $w*0.45, $y - $h*0.4, $w*0.9, $h*0.4)
    $e = [Math]::Max(1.5, $w * 0.09)
    $g.FillEllipse((Br (C 40 30 50)), $x - $w*0.2 - $e/2, $y - $h*0.6, $e, $e*1.3)
    $g.FillEllipse((Br (C 40 30 50)), $x + $w*0.2 - $e/2, $y - $h*0.6, $e, $e*1.3)
    $g.FillEllipse((Br (C 255 150 180 160)), $x - $w*0.36, $y - $h*0.42, $e*1.4, $e*0.8)
    $g.FillEllipse((Br (C 255 150 180 160)), $x + $w*0.36 - $e*1.4, $y - $h*0.42, $e*1.4, $e*0.8)
    $g.FillEllipse((Br (C 255 255 255 200)), $x - $w*0.3, $y - $h*0.92, $w*0.2, $h*0.14)
}

# 위 줄 3번째 칸: 슬라임 인형 방 (연보라 벽, 나무 바닥, 분홍 러그, 큰 민트 슬라임, 선반 위 작은 슬라임들)
$ox = 260; $oy = 4
Room $ox $oy (C 236 228 242) (C 196 176 222) (C 214 198 234) (C 150 108 78)
$g.FillEllipse((Br (C 232 150 186)), $ox + 30, $oy + 96, 60, 18)
$g.FillRectangle((Br (C 120 86 62)), $ox + 36, $oy + 50, 48, 3)
Slime ($ox + 44) ($oy + 50) 9 7 (C 255 140 180)
Slime ($ox + 60) ($oy + 50) 9 7 (C 120 180 255)
Slime ($ox + 76) ($oy + 50) 9 7 (C 255 215 90)
Slime ($ox + 60) ($oy + 100) 36 28 (C 110 214 176)

# 위 줄 4번째 칸: MALLANG 포스터 방 (복숭아색 벽, 보라 카펫, 포스터, 카드 상자 진열 책상)
$ox = 388; $oy = 4
Room $ox $oy (C 242 232 222) (C 222 190 166) (C 232 204 180) (C 112 98 136)
$g.FillRectangle((Br (C 74 56 114)), $ox + 41, $oy + 35, 38, 40)
$g.FillRectangle((Br (C 255 140 190)), $ox + 41, $oy + 35, 38, 3)
Slime ($ox + 60) ($oy + 64) 20 16 (C 255 140 190)
$f = New-Object System.Drawing.Font 'Arial', 5.5, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$sf = New-Object System.Drawing.StringFormat; $sf.Alignment = 'Center'
$g.DrawString('MALLANG', $f, (Br (C 255 210 90)), (New-Object System.Drawing.RectangleF ($ox + 41), ($oy + 67), 38, 8), $sf)
$g.FillRectangle((Br (C 120 80 56)), $ox + 64, $oy + 78, 24, 4)
$g.FillRectangle((Br (C 100 66 46)), $ox + 66, $oy + 82, 3, 9)
$g.FillRectangle((Br (C 100 66 46)), $ox + 83, $oy + 82, 3, 9)
$g.FillRectangle((Br (C 255 120 170)), $ox + 66, $oy + 70, 7, 8)
$g.FillRectangle((Br (C 255 205 80)), $ox + 74, $oy + 72, 6, 6)
$g.FillRectangle((Br (C 130 200 255)), $ox + 81, $oy + 71, 6, 7)

# 아래 줄 오른쪽 두 칸: MALLANG 네온 간판 (248 × 120)
$ox = 260; $oy = 132; $w = 248; $h = 120
$g.FillRectangle((Br (C 26 16 36)), 256, 128, 256, 128)
$g.FillRectangle((Br (C 38 24 54)), $ox + 4, $oy + 4, $w - 8, $h - 8)
foreach ($p in @(@(10, 40), @(6, 80), @(2.5, 255))) {
    $pen = New-Object System.Drawing.Pen ((C 255 110 180 $p[1])), $p[0]
    $g.DrawRectangle($pen, $ox + 8, $oy + 8, $w - 16, $h - 16)
}
function Neon($text, $size, $cx, $cy, $glow, $core) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $fam = New-Object System.Drawing.FontFamily 'Arial'
    $sf2 = New-Object System.Drawing.StringFormat; $sf2.Alignment = 'Center'; $sf2.LineAlignment = 'Center'
    $path.AddString($text, $fam, [int][System.Drawing.FontStyle]::Bold, $size, (New-Object System.Drawing.PointF $cx, $cy), $sf2)
    foreach ($p in @(@(9, 35), @(5, 70), @(2.5, 140))) {
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb($p[1], $glow.R, $glow.G, $glow.B)), $p[0]
        $pen.LineJoin = 'Round'
        $g.DrawPath($pen, $path)
    }
    $g.FillPath((Br $core), $path)
}
Neon 'MALLANG' 38 ($ox + $w / 2) ($oy + 50) (C 255 90 170) (C 255 220 238)
Neon 'SLIME CARDS' 17 ($ox + $w / 2) ($oy + 89) (C 255 190 60) (C 255 236 180)

$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$img.Dispose(); $bmp.Dispose()
"saved $Out"

