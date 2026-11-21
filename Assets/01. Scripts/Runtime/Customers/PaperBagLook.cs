using CardShop.Core;
using CardShop.UI;
using TMPro;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 계산대 종이봉투의 새 모양 (11-22 G2: 디자인이 밋밋함). 씬을 다시 만들지 않게 실행할 때 봉투(PaperBag)에 스스로 붙어
    /// 예전 상자 5장(Bottom · Wall*)을 숨기고 새로 만듭니다: 크래프트 벽 + 윗단을 접은 밝은 띠 + 뒤쪽 손잡이 끈 + 앞면 MALLANG 로고와 분홍 슬라임.
    /// 폭을 28 → 34cm로 넓혀 상자(20cm) 옆에 피규어가 섬 (11-22 G1). 플레이어 쪽(-Z) 앞벽은 예전처럼 낮게(안이 보이게).
    /// 재질은 예전 벽 재질을 복사해 색만 바꿈 — 빌드에서 기본 재질 셰이더가 빠져 분홍이 되는 것을 피함(B1과 같은 방식).
    /// </summary>
    public sealed class PaperBagLook : MonoBehaviour
    {
        static readonly Vector3 Inner = new Vector3(0.34f, 0.2f, 0.19f);
        const float Wall = 0.004f;
        const float FrontRatio = 0.4f; // 앞벽 높이 = 안쪽 높이 × 이 값
        const float Cuff = 0.025f;     // 접은 띠 높이
        static readonly Color Kraft = new Color(0.66f, 0.48f, 0.3f);
        static readonly Color CuffColor = new Color(0.82f, 0.68f, 0.5f);
        static readonly Color Cord = new Color(0.93f, 0.88f, 0.78f);
        static readonly Color SlimePink = new Color(1f, 0.55f, 0.72f);
        static readonly Color Ink = new Color(0.17f, 0.13f, 0.25f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            foreach (PaperBag bag in FindObjectsByType<PaperBag>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (bag.GetComponent<PaperBagLook>() == null) bag.gameObject.AddComponent<PaperBagLook>();
        }

        void Awake()
        {
            Material source = null;
            foreach (string part in new[] { "Bottom", "WallFront", "WallBack", "WallLeft", "WallRight" })
            {
                Transform t = transform.Find(part);
                if (t == null || !t.TryGetComponent(out MeshRenderer r)) continue;
                if (source == null) source = r.sharedMaterial;
                r.enabled = false;
            }
            if (source == null) return; // 예전 모양을 못 찾으면 손대지 않음 (크기도 그대로)

            GetComponent<PaperBag>().SetInnerSize(Inner);
            var root = new GameObject("BagLook").transform;
            root.SetParent(transform, false);
            Build(root, source);
        }

        void Build(Transform root, Material source)
        {
            Material kraft = Tint(source, Kraft), cuff = Tint(source, CuffColor), cord = Tint(source, Cord);
            float w = Inner.x + 2f * Wall, h = Inner.y, d = Inner.z, front = h * FrontRatio;
            float zBack = (d + Wall) / 2f, zFront = -zBack, xSide = (Inner.x + Wall) / 2f;

            Box(root, "Bottom", new Vector3(0f, -Wall / 2f, 0f), new Vector3(w, Wall, d + 2f * Wall), kraft);
            Box(root, "WallBack", new Vector3(0f, h / 2f, zBack), new Vector3(w, h, Wall), kraft);
            Box(root, "WallFront", new Vector3(0f, front / 2f, zFront), new Vector3(w, front, Wall), kraft);
            // 옆벽은 뒤에서 앞으로 비스듬히 낮아지는 대신 두 단: 뒤 절반은 높게, 앞 절반은 앞벽 높이 (안이 보이면서 봉투 옆선이 남음)
            for (int s = -1; s <= 1; s += 2)
            {
                Box(root, "WallSideBack", new Vector3(s * xSide, h / 2f, d / 4f), new Vector3(Wall, h, d / 2f), kraft);
                Box(root, "WallSideFront", new Vector3(s * xSide, front / 2f, -d / 4f), new Vector3(Wall, front, d / 2f), kraft);
            }

            // 윗단을 바깥으로 접은 밝은 띠 (벽보다 살짝 두껍게)
            float t2 = Wall * 2.5f;
            Box(root, "CuffBack", new Vector3(0f, h - Cuff / 2f, zBack + Wall), new Vector3(w + t2, Cuff, t2), cuff);
            Box(root, "CuffFront", new Vector3(0f, front - Cuff / 2f, zFront - Wall), new Vector3(w + t2, Cuff, t2), cuff);
            for (int s = -1; s <= 1; s += 2)
            {
                Box(root, "CuffSideBack", new Vector3(s * (xSide + Wall), h - Cuff / 2f, d / 4f), new Vector3(t2, Cuff, d / 2f), cuff);
                Box(root, "CuffSideFront", new Vector3(s * (xSide + Wall), front - Cuff / 2f, -d / 4f), new Vector3(t2, Cuff, d / 2f), cuff);
            }

            // 뒤쪽 손잡이: 뒷벽 위로 둥근 끈 (작은 막대 9개로 반원)
            const int segments = 9;
            const float radius = 0.06f, thick = 0.007f;
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.PI * (i + 0.5f) / segments;
                var p = new Vector3(Mathf.Cos(a) * radius, h + Mathf.Sin(a) * radius * 0.9f, zBack);
                GameObject seg = RuntimeShapes.Create(PrimitiveType.Cube, "Handle", root, p, new Vector3(Mathf.PI * radius / segments + 0.002f, thick, thick), cord, castShadows: false);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
            }

            Logo(root, zFront - Wall, front, Tint(source, SlimePink), Tint(source, Ink));
        }

        // 앞벽 바깥(-Z) 로고: 분홍 슬라임 얼굴 + MALLANG 글자 (팩 앞면과 같은 브랜드)
        void Logo(Transform root, float z, float frontH, Material pink, Material ink)
        {
            float y = frontH * 0.5f;
            Vector3 slime = new Vector3(-0.1f, y, z - 0.004f);
            RuntimeShapes.Create(PrimitiveType.Sphere, "LogoSlime", root, slime, new Vector3(0.05f, 0.04f, 0.012f), pink, castShadows: false);
            for (int s = -1; s <= 1; s += 2)
                RuntimeShapes.Create(PrimitiveType.Sphere, "LogoEye", root, slime + new Vector3(s * 0.009f, 0.002f, -0.006f), Vector3.one * 0.007f, ink, castShadows: false);

            var go = new GameObject("LogoText");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0.035f, y, z - 0.002f);
            var text = go.AddComponent<TextMeshPro>();
            if (WorldUiKit.KoreanFont != null) text.font = WorldUiKit.KoreanFont;
            text.text = "MALLANG";
            text.fontSize = 0.35f; // TMP 3D 글자: 1 ≈ 10cm → 글자 높이 약 3cm
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 6f;
            text.color = new Color(1f, 0.97f, 0.9f);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(0.22f, 0.05f);
        }

        static void Box(Transform root, string name, Vector3 pos, Vector3 size, Material mat) =>
            RuntimeShapes.Create(PrimitiveType.Cube, name, root, pos, size, mat, castShadows: false);

        static Material Tint(Material source, Color color)
        {
            var m = new Material(source);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }
    }
}
