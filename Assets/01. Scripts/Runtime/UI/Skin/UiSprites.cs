using System.Collections.Generic;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// UI 재질 스프라이트를 코드로 만듭니다 (UI_개편_설계 6장 1단계). 그림 파일 없이 둥근 판 · 테두리 · 원 · 영수증 톱니를 만들고 기억합니다.
    /// 둥근 판은 9-slice(모서리 border)라 어떤 크기로 늘려도 모서리가 찌그러지지 않습니다.
    /// 모서리 크기: uGUI는 <see cref="UnityEngine.UI.Image.pixelsPerUnitMultiplier"/>로, SpriteRenderer는 ppu로 맞춥니다.
    /// </summary>
    public static class UiSprites
    {
        /// <summary>둥근 판 · 테두리 스프라이트의 모서리 반지름 (픽셀)</summary>
        public const int Radius = 32;

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>속이 찬 둥근 사각형. ppu 100이면 모서리 32mm(uGUI 1px = 1mm 캔버스 기준)</summary>
        public static Sprite Rounded(float pixelsPerUnit = 100f) =>
            Get("rounded" + pixelsPerUnit, () => Make(Radius * 2 + 2, Radius * 2 + 2, (x, y, w, h) => RoundedCoverage(x, y, w, h, Radius), pixelsPerUnit, Radius));

        /// <summary>둥근 사각형 테두리만 (두께 thickness 픽셀)</summary>
        public static Sprite Frame(int thickness, float pixelsPerUnit = 100f) =>
            Get($"frame{thickness}_{pixelsPerUnit}", () => Make(Radius * 2 + 2, Radius * 2 + 2,
                (x, y, w, h) => RoundedCoverage(x, y, w, h, Radius) * (1f - InsetCoverage(x, y, w, h, Radius, thickness)), pixelsPerUnit, Radius));

        /// <summary>점선 한 칸 길이 (픽셀): 선 DashPeriod/2 + 빈칸 DashPeriod/2</summary>
        public const int DashPeriod = 16;

        /// <summary>
        /// 둥근 사각형 점선 테두리 (혼잣말 말풍선). 모서리는 실선, 곧은 변만 점선.
        /// 가운데 한 칸(DashPeriod)을 반복해야 점이 늘어나지 않으므로 SpriteRenderer는 <see cref="SpriteDrawMode.Tiled"/>로 씁니다.
        /// </summary>
        public static Sprite DashedFrame(int thickness, float pixelsPerUnit = 100f) =>
            Get($"dashed{thickness}_{pixelsPerUnit}", () => Make(Radius * 2 + DashPeriod, Radius * 2 + DashPeriod,
                (x, y, w, h) => RoundedCoverage(x, y, w, h, Radius) * (1f - InsetCoverage(x, y, w, h, Radius, thickness)) * DashMask(x, y, w, h, Radius),
                pixelsPerUnit, Radius));

        /// <summary>원 (도장 바탕 · 아이콘)</summary>
        public static Sprite Circle() => Get("circle", () => Make(128, 128, (x, y, w, h) => RoundedCoverage(x, y, w, h, 64), 100f, 0));

        /// <summary>고리 (도장 테두리)</summary>
        public static Sprite Ring(int thickness) =>
            Get("ring" + thickness, () => Make(128, 128, (x, y, w, h) => RoundedCoverage(x, y, w, h, 64) * (1f - InsetCoverage(x, y, w, h, 64, thickness)), 100f, 0));

        /// <summary>체크 표시 ✓ (글꼴에 ✓가 없어도 깨지지 않게 그림으로, 알림 좋은 소식 아이콘)</summary>
        public static Sprite Check() => Get("check", () => Make(64, 64, CheckCoverage, 100f, 0));

        /// <summary>✓ 두 획: (14,34)→(27,20)→(50,46), 두께 7픽셀</summary>
        public static float CheckCoverage(int x, int y, int w, int h)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            float d = Mathf.Min(SegmentDistance(p, new Vector2(14f, 34f), new Vector2(27f, 20f)),
                                SegmentDistance(p, new Vector2(27f, 20f), new Vector2(50f, 46f)));
            return Mathf.Clamp01(4f - d); // 반두께 3.5 + 0.5 부드럽게
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>영수증 아래 톱니 한 칸 (16 × 8, 아래로 뾰족). Image.Type.Tiled로 가로로 반복</summary>
        public static Sprite TearEdge() => Get("tear", () => Make(16, 8, TearCoverage, 100f, 0, TextureWrapMode.Repeat));

        /// <summary>
        /// 둥근 사각형 둘레가 부드럽게 사라지는 빛 (팩 결과 수퍼레어 이상 카드 뒤). 9-slice라 카드보다 조금 크게 늘려 씀.
        /// 블룸 셰이더 대신 스프라이트 알파로 (퀘스트 성능, UI_개편_설계 6장).
        /// </summary>
        public static Sprite Glow(float pixelsPerUnit = 100f) =>
            Get("glow" + pixelsPerUnit, () => Make(Radius * 2 + 2, Radius * 2 + 2, GlowCoverage, pixelsPerUnit, Radius));

        /// <summary>가운데가 진하고 좌우로 사라지는 세로 띠 (판을 지나가는 빛줄기). Sliced(border 0)로 크기를 정해 씀</summary>
        public static Sprite SoftBand() => Get("band", () => Make(32, 8, (x, y, w, h) =>
        {
            float u = (x + 0.5f) / w * 2f - 1f; // -1 ~ 1
            return Mathf.Exp(-u * u * 4f);
        }, 100f, 0));

        // ---- 슬라임 카드 문양 (11-22 "카드 문양 기준"): 앞면 등급 마크 · 뒷면 돔 무늬와 같은 모양

        /// <summary>슬라임 돔 (등급 마크, 64 × 48, 아래 가운데 기준 반타원). SpriteRenderer 색으로 등급 색을 입힘</summary>
        public static Sprite Dome() => Get("dome", () => Make(64, 48, DomeCoverage, 100f, 0));

        /// <summary>레전더리 무지개 돔 (색이 그림에 들어 있어 흰색으로 씀)</summary>
        public static Sprite RainbowDome() => Get("domeRainbow", () => MakeRgba(64, 48, (x, y, w, h) =>
        {
            Color c = Color.HSVToRGB(Mathf.Repeat(0.02f + (x + 0.5f) / w * 0.62f, 1f), 0.45f, 1f); // 빨강 → 파랑, 파스텔
            c.a = DomeCoverage(x, y, w, h);
            return c;
        }));

        /// <summary>네 갈래 반짝임 (유니크 · 레전더리 마크 옆, 카드 셰이더 SparkMask와 같은 모양)</summary>
        public static Sprite Sparkle() => Get("sparkle", () => Make(64, 64, (x, y, w, h) =>
        {
            float ax = Mathf.Abs((x + 0.5f) / w * 2f - 1f), ay = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
            float s = Mathf.Sqrt(ax) + Mathf.Sqrt(ay);
            return Mathf.Clamp01((1f - s) * 12f + 0.5f);
        }, 100f, 0));

        /// <summary>카드 뒷면의 옅은 돔 무늬 한 칸 (60 × 52, 돔 둘이 엇갈림). SpriteRenderer Tiled로 판 전체에 반복</summary>
        public static Sprite DomePattern(float pixelsPerUnit) => Get("domePattern" + pixelsPerUnit, () => Make(60, 52, (x, y, w, h) =>
            Mathf.Max(DomeAt(x, y, 8, 29, 20, 15), DomeAt(x, y, 38, 3, 20, 15)), pixelsPerUnit, 0, TextureWrapMode.Repeat));

        // ------------------------------------------------------------------ 계산 (테스트용으로 열어 둠)

        /// <summary>w × h 칸 아래 가운데에 놓인 돔 (폭 w-4, 높이 h-4)</summary>
        public static float DomeCoverage(int x, int y, int w, int h) => DomeAt(x, y, 2, 2, w - 4, h - 4);

        /// <summary>왼쪽 아래 (left, bottom)에서 폭 dw · 높이 dh인 돔(아래가 평평한 반타원)의 덮임 0~1</summary>
        public static float DomeAt(int x, int y, float left, float bottom, float dw, float dh)
        {
            float px = x + 0.5f, py = y + 0.5f;
            float qx = (px - (left + dw * 0.5f)) / (dw * 0.5f), qy = (py - bottom) / dh;
            float d = (Mathf.Sqrt(qx * qx + qy * qy) - 1f) * Mathf.Min(dw * 0.5f, dh); // 둥근 윗부분까지 거리 (픽셀 정도)
            return Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(py - bottom + 0.5f);
        }

        /// <summary>안쪽(모서리 반지름만큼 줄인 사각형)은 1, 거기서 Radius 픽셀 멀어질수록 제곱으로 0까지</summary>
        public static float GlowCoverage(int x, int y, int w, int h)
        {
            float px = x + 0.5f, py = y + 0.5f;
            float dx = px - Mathf.Clamp(px, Radius, w - Radius);
            float dy = py - Mathf.Clamp(py, Radius, h - Radius);
            float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / Radius);
            return t * t;
        }

        /// <summary>
        /// w × h 사각형(모서리 반지름 r) 안에 픽셀 (x, y)가 얼마나 덮이는지 0~1. 픽셀 가운데(x+0.5)로 재고 경계 1픽셀을 부드럽게(안티에일리어싱).
        /// </summary>
        public static float RoundedCoverage(int x, int y, int w, int h, float r)
        {
            float px = x + 0.5f, py = y + 0.5f;
            // 가장 가까운 모서리 원 중심까지 (모서리 바깥 구역일 때만)
            float cx = Mathf.Clamp(px, r, w - r);
            float cy = Mathf.Clamp(py, r, h - r);
            float dx = px - cx, dy = py - cy;
            float outside = Mathf.Sqrt(dx * dx + dy * dy) - r; // 0 미만이면 안쪽
            // 사각형 변 바깥은 위 계산이 이미 처리 (clamp 범위 밖이면 거리 > r)
            return Mathf.Clamp01(0.5f - outside);
        }

        /// <summary>테두리 두께만큼 안쪽으로 줄인 둥근 사각형의 덮임 (테두리 = 바깥 − 이것)</summary>
        public static float InsetCoverage(int x, int y, int w, int h, float r, int inset)
        {
            float px = x + 0.5f - inset, py = y + 0.5f - inset;
            float iw = w - inset * 2, ih = h - inset * 2, ir = Mathf.Max(0f, r - inset);
            if (iw <= 0f || ih <= 0f) return 0f;
            float cx = Mathf.Clamp(px, ir, iw - ir);
            float cy = Mathf.Clamp(py, ir, ih - ir);
            float dx = px - cx, dy = py - cy;
            float outside = Mathf.Sqrt(dx * dx + dy * dy) - ir;
            return Mathf.Clamp01(0.5f - outside);
        }

        /// <summary>점선 무늬: 곧은 변(모서리 border 사이)에서 DashPeriod마다 앞 절반만 1, 모서리는 늘 1</summary>
        public static float DashMask(int x, int y, int w, int h, int border)
        {
            bool midX = x >= border && x < w - border;
            bool midY = y >= border && y < h - border;
            if (midX == midY) return 1f; // 모서리 (또는 안쪽 빈 곳)
            int along = midX ? x - border : y - border;
            return along % DashPeriod < DashPeriod / 2 ? 1f : 0f;
        }

        /// <summary>톱니: 위쪽은 꽉 차고, 아래로 갈수록 가운데만 남는 삼각형</summary>
        public static float TearCoverage(int x, int y, int w, int h)
        {
            float half = w * 0.5f;
            float fromCenter = Mathf.Abs(x + 0.5f - half);           // 0 ~ half
            float allowed = half * ((y + 0.5f) / h);                 // 아래(y=0)는 0, 위는 half
            return Mathf.Clamp01(allowed - fromCenter + 0.5f);
        }

        // ------------------------------------------------------------------ 만들기

        delegate float Coverage(int x, int y, int w, int h);

        static Sprite Get(string key, System.Func<Sprite> make)
        {
            if (cache.TryGetValue(key, out Sprite s) && s != null) return s;
            return cache[key] = make();
        }

        /// <summary>색이 들어간 그림 (무지개 돔). 나머지는 <see cref="Make"/>처럼 흰색 + 알파</summary>
        static Sprite MakeRgba(int w, int h, System.Func<int, int, int, int, Color> pixel)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "UiSprite", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = pixel(x, y, w, h);
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        static Sprite Make(int w, int h, Coverage coverage, float ppu, int border, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "UiSprite",
                filterMode = FilterMode.Bilinear,
                wrapMode = wrap,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage(x, y, w, h) * 255f));
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
