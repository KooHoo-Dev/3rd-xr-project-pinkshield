<#
  클립보드 이미지를 DevLog 스크린샷으로 저장합니다. (Win+Shift+S 캡처 → 이 스크립트)

  사용:
    powershell -File DevLog/tools/devlog-paste.ps1                       # screenshots/_inbox/<시각>.png
    powershell -File DevLog/tools/devlog-paste.ps1 -Bug BUG-003 -Name after
#>
param(
    [string]$Bug = "",
    [string]$Name = ""
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$img = [System.Windows.Forms.Clipboard]::GetImage()
if ($null -eq $img) {
    Write-Error "클립보드에 이미지가 없습니다. Win+Shift+S로 먼저 캡처하세요."
    exit 1
}

$root = Split-Path -Parent $PSScriptRoot          # DevLog/
if ($Bug -ne "") {
    if ($Bug -notmatch '^BUG-\d{3}$') { Write-Error "-Bug 는 BUG-001 형식이어야 합니다."; exit 1 }
    $dir = Join-Path $root "screenshots\$Bug"
} else {
    $dir = Join-Path $root "screenshots\_inbox"
}
New-Item -ItemType Directory -Force -Path $dir | Out-Null

if ($Name -eq "") { $Name = Get-Date -Format "yyyyMMdd-HHmmss" }
$Name = $Name -replace '\.png$', ''

# 같은 이름이 있으면 -2, -3 … 을 붙인다
$path = Join-Path $dir "$Name.png"
$i = 2
while (Test-Path $path) { $path = Join-Path $dir "$Name-$i.png"; $i++ }

$img.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$img.Dispose()

$rel = $path.Substring($root.Length + 1) -replace '\\', '/'
Write-Output "저장: DevLog/$rel"
