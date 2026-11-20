using CardShop.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardShop.Presentation
{
    /// <summary>
    /// "꾹 누르기(1.5초) = 건너뛰기" 게이지 (시연 피드백 B3 · UI_개편_설계 C안: 원형 게이지). 눌림 시간이 보이지 않으면 고장으로 오해하므로,
    /// 꾹 누르는 동안 카드 묶음 아래에 카드 재질(진보라 + 금박) 둥근 판이 점으로 한 바퀴 차오릅니다. 문구는 그 아래 작은 판.
    /// 월드 고정(머리에 붙이지 않음, 두 눈 겹침 방지)이고 스프라이트 몇십 개 + TMP 한 줄이라 가볍습니다.
    /// 앞면(-Z)이 플레이어 쪽인 회전을 받습니다(카드 묶음과 같은 회전).
    /// </summary>
    public sealed class SkipHoldGauge : MonoBehaviour
    {
        const float Diameter = 0.07f;     // 둥근 판 지름 (m)
        const float DotRing = 0.027f;     // 점이 늘어선 원의 반지름 (m)
        const float DotSize = 0.0085f;    // 점 지름 (m)
        const int DotCount = 24;
        const float CircleUnit = 1.28f;   // UiSprites.Circle/Ring 한 장의 크기 (128px, 100px = 1유닛)

        // 아직 안 찬 점: 금색을 흐리게 (새 색 토큰 없이 UiTheme.Gold의 알파만 낮춤)
        static readonly Color DotOff = new Color(0.29f, 0.22f, 0.45f); // #4A3873 아직 안 찬 칸 (샘플 5번 원형 게이지)

        SpriteRenderer[] dots;

        public static SkipHoldGauge Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("SkipHoldGauge");
            go.transform.SetParent(parent, false);
            var gauge = go.AddComponent<SkipHoldGauge>();

            // 둥근 판(진보라) + 금박 테
            float scale = Diameter / CircleUnit;
            RevealSprites.Quad(go.transform, "Disc", UiSprites.Circle(), Vector3.zero, new Vector2(scale, scale), UiTheme.CardBack, 0);
            RevealSprites.Quad(go.transform, "Rim", UiSprites.Ring(6), new Vector3(0f, 0f, -0.0003f), new Vector2(scale, scale), UiTheme.Gold, 1);

            // 점 24개를 위에서부터 시계 방향으로 채움 (점은 한 번 만들어 색만 바꿈)
            gauge.dots = new SpriteRenderer[DotCount];
            float dotScale = DotSize / CircleUnit;
            for (int i = 0; i < DotCount; i++)
            {
                float a = i * Mathf.PI * 2f / DotCount;
                var pos = new Vector3(Mathf.Sin(a) * DotRing, Mathf.Cos(a) * DotRing, -0.0006f);
                gauge.dots[i] = RevealSprites.Quad(go.transform, "Dot" + i, UiSprites.Circle(), pos, new Vector2(dotScale, dotScale), DotOff, 2);
            }

            // 문구는 둥근 판 아래 작은 카드 재질 판 위에 (예전 문구 그대로)
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<TextMeshPro>();
            if (font != null) label.font = font;
            label.text = "꾹 누르면 건너뛰기"; // 11-20: "갓팩은 보여 줘요" 뺌 (사용자 요청)
            label.fontSize = 0.09f; // 1 ≈ 10cm → 글자 약 9mm
            label.alignment = TextAlignmentOptions.Top;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var rt = (RectTransform)labelGo.transform;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0.3f, 0.03f);
            rt.localPosition = new Vector3(0f, -Diameter * 0.5f - 0.014f, -0.0008f);
            TextPlate plate = TextPlate.Attach(label, UiTheme.CardBack, new Vector2(0.012f, 0.007f), 0.008f, UiTheme.Gold, 0.002f, sortBase: 3);
            plate.Fit();

            go.SetActive(false);
            return gauge;
        }

        /// <param name="progress">0~1</param>
        public void Show(Pose pose, float progress)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            float p = Mathf.Clamp01(progress);
            int lit = Mathf.CeilToInt(p * DotCount);
            Color on = UiTheme.Gold; // 샘플 5번: 금색으로 차오름
            for (int i = 0; i < dots.Length; i++) dots[i].color = i < lit ? on : DotOff;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
