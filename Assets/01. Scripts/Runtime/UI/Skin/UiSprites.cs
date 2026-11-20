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

        /// <summary>원 (도장 바탕 · 아이콘)</summary>
        public static Sprite Circle() => Get("circle", () => Make(128, 128, (x, y, w, h) => RoundedCoverage(x, y, w, h, 64), 100f, 0));

        /// <summary>고리 (도장 테두리)</summary>
        public static Sprite Ring(int thickness) =>
            Get("ring" + thickness, () => Make(128, 128, (x, y, w, h) => RoundedCoverage(x, y, w, h, 64) * (1f - InsetCoverage(x, y, w, h, 64, thickness)), 100f, 0));

        /// <summary>영수증 아래 톱니 한 칸 (16 × 8, 아래로 뾰족). Image.Type.Tiled로 가로로 반복</summary>
        public static Sprite TearEdge() => Get("tear", () => Make(16, 8, TearCoverage, 100f, 0, TextureWrapMode.Repeat));

        // ------------------------------------------------------------------ 계산 (테스트용으로 열어 둠)

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
