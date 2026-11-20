<#
도장 그림 → 잉크 PNG (흰 종이 위 검은 선 그림 → 흰 선 + 투명 배경)

card-nukki.ps1은 테두리에 이어진 배경만 지워 얼굴 안쪽 흰색이 남으므로, 한 색 도장에는 이 도구를 씁니다.
선의 어두운 정도를 그대로 투명도로 바꿉니다 (검정 = 불투명, 흰 종이 = 투명, 가장자리 회색 = 반투명).
색은 흰색으로 두어 게임에서 등급별 잉크 색을 곱해 물들입니다 (플레이피드백_11-20_설계 5-1 · Q8).

예)
  powershell -File DevLog/tools/stamp-ink.ps1 -Src ~/Downloads/스탬프/Stamp_Great.png -Out "Assets/Resources/Stamps/Stamp_Great.png"
  -Paper: 이 밝기 이상은 완전히 투명 (종이 얼룩 정리, 기본 235)
  -Ink:   이 밝기 이하는 완전히 불투명 (기본 40)
#>
param(
    [Parameter(Mandatory = $true)][string]$Src,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$Paper = 235,
    [int]$Ink = 40
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$Src = (Resolve-Path $Src).Path
$srcBmp = [Drawing.Bitmap]::FromFile($Src)
$w = $srcBmp.Width; $h = $srcBmp.Height
$dst = New-Object Drawing.Bitmap $w, $h, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
$rect = New-Object Drawing.Rectangle 0, 0, $w, $h
# 24bpp/32bpp 어느 쪽이든 32bpp로 복사해 한 번에 읽음
$tmp = $srcBmp.Clone($rect, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
$srcBmp.Dispose()
$sd = $tmp.LockBits($rect, 'ReadOnly', [Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bytes = New-Object byte[] ($sd.Stride * $h)
[Runtime.InteropServices.Marshal]::Copy($sd.Scan0, $bytes, 0, $bytes.Length)
$tmp.UnlockBits($sd); $tmp.Dispose()

$opaque = 0
for ($i = 0; $i -lt $bytes.Length; $i += 4) {
    $b = $bytes[$i]; $g = $bytes[$i + 1]; $r = $bytes[$i + 2]; $a0 = $bytes[$i + 3]
    $lum = 0.299 * $r + 0.587 * $g + 0.114 * $b
    $t = ($Paper - $lum) / ($Paper - $Ink)          # 1 = 잉크, 0 = 종이
    if ($t -lt 0) { $t = 0 } elseif ($t -gt 1) { $t = 1 }
    $a = [int][Math]::Round($t * $a0)
    if ($a -ge 250) { $opaque++ }
    $bytes[$i] = 255; $bytes[$i + 1] = 255; $bytes[$i + 2] = 255; $bytes[$i + 3] = [byte]$a
}

$dd = $dst.LockBits($rect, 'WriteOnly', [Drawing.Imaging.PixelFormat]::Format32bppArgb)
[Runtime.InteropServices.Marshal]::Copy($bytes, 0, $dd.Scan0, $bytes.Length)
$dst.UnlockBits($dd)
$outFull = [IO.Path]::GetFullPath($Out)
New-Item -ItemType Directory -Force (Split-Path $outFull) | Out-Null
$dst.Save($outFull, [Drawing.Imaging.ImageFormat]::Png)
$dst.Dispose()
"잉크 픽셀 $opaque 개 → $outFull"
