<#
카드 캐릭터 그림 누끼 도구 (Gemini 생성 이미지 → 투명 PNG)

  -Mode dark    어두운 단색 배경(공기 속성 등) 제거 + 갇힌 배경 덩어리 제거
  -Mode light   흰색·밝은 회색 배경 제거 → 이어서 바닥 그림자 제거 → 바닥의 작은 점 제거
  -Mode shadow  이미 투명한 그림에서 바닥 그림자(불투명 회색 타원)만 제거

워터마크(Gemini ✦, 1024px 기준 오른쪽 아래 약 905,905)는 dark·light에서 자동으로 지웁니다.
출력 이름은 카드 규칙대로 <ID>_char.png (예: A-SR → A_SR_char.png)로 맞춥니다.

예)
  powershell -File DevLog/tools/card-nukki.ps1 -Src ~/Downloads/A-SR.png -Mode dark
  powershell -File DevLog/tools/card-nukki.ps1 -Src ~/Downloads/E-SR.png -Mode light
  powershell -File DevLog/tools/card-nukki.ps1 -Src ~/Downloads/W-N1.png -Mode shadow
  # 결과는 기본으로 Assets/04. Art/Cards/Char/ 에 저장. -OutDir로 바꿀 수 있음(먼저 임시 폴더에 뽑아 눈으로 확인 권장)

그림자 기준값(-SatMax·-LumMin·-LumMax·-Band·-SatLow·-Protect)은 그림마다 조정이 필요할 수 있습니다.
2026-11-17에 쓴 값:
  기본           SatMax 0.30, LumMin 90, LumMax 255, Band 200, SatLow 0.30
  W-N2(비눗방울)  SatMax 0.42, Band 100, SatLow 0.42, Protect 580,460,840,660
  W-L(파란 그림자) SatMax 0.55, LumMin 50, LumMax 225, Band 140, SatLow 0
  F-L(주황 그림자) SatMax 0.60, LumMin 80, LumMax 212, Band 200, SatLow 0.35
  E-SR(light)     그림자 단계 LumMin 150 (회색 돌 조각 보호)
#>
param(
    [Parameter(Mandatory = $true)][string]$Src,
    [ValidateSet('dark', 'light', 'shadow')][string]$Mode = 'shadow',
    [string]$OutDir = '',
    [string]$Id = '',
    [double]$SatMax = 0.30,
    [int]$LumMin = 90,
    [int]$LumMax = 255,
    [int]$Band = 200,
    [double]$SatLow = -1,
    [int[]]$Protect = @(-1, -1, -1, -1)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$here = $PSScriptRoot
foreach ($name in 'BgCut', 'ShadowCut') {
    if (-not ($name -as [type])) {
        Add-Type -TypeDefinition (Get-Content (Join-Path $here "$name.cs") -Raw -Encoding UTF8) -ReferencedAssemblies System.Drawing
    }
}

$Src = (Resolve-Path $Src).Path
if (-not $Id) { $Id = [IO.Path]::GetFileNameWithoutExtension($Src) -replace '_char$', '' }
$fileId = $Id.Replace('-', '_')
if (-not $OutDir) { $OutDir = Join-Path $here '..\..\Assets\04. Art\Cards\Char' }
New-Item -ItemType Directory -Force $OutDir | Out-Null
$dst = Join-Path (Resolve-Path $OutDir).Path "${fileId}_char.png"
if ($SatLow -lt 0) { $SatLow = $SatMax }
$tmp1 = [IO.Path]::GetTempFileName() + '.png'
$tmp2 = [IO.Path]::GetTempFileName() + '.png'

switch ($Mode) {
    'dark' {
        # 배경 밝기 0~90, 채도 0.2 이하. 가장자리 90 거리로 알파. 80px 이상 갇힌 배경도 제거
        $n = [BgCut]::Run($Src, $dst, 0, 90, 0.2, 90.0, 870, 870, 945, 945, 80, -1, 0)
        "배경 제거 $n px → $dst"
    }
    'light' {
        $n = [BgCut]::Run($Src, $tmp1, 205, 255, 0.06, 50.0, 870, 870, 945, 945, 0, -1, 0)
        "배경 제거 $n px"
        $lm = [Math]::Max($LumMin, 150)
        $n = [ShadowCut]::Run($tmp1, $tmp2, $SatMax, $lm, $LumMax, $Band, 4, $SatLow, $Protect[0], $Protect[1], $Protect[2], $Protect[3])
        "그림자 제거 $n px"
        $n = [BgCut]::Run($tmp2, $dst, 256, 256, 0, 1.0, -1, -1, -1, -1, 0, 800, 40)
        "작은 점 정리 → $dst"
    }
    'shadow' {
        $n = [ShadowCut]::Run($Src, $dst, $SatMax, $LumMin, $LumMax, $Band, 4, $SatLow, $Protect[0], $Protect[1], $Protect[2], $Protect[3])
        "그림자 제거 $n px → $dst"
    }
}
Remove-Item $tmp1, $tmp2 -ErrorAction SilentlyContinue
"다음: Unity에서 Tools > CardShop > Import Cards CSV"
