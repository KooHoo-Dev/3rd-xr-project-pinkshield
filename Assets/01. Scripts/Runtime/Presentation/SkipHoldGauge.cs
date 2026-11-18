using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardShop.Presentation
{
    /// <summary>
    /// "꾹 누르기(1.5초) = 건너뛰기" 게이지 (시연 피드백 B3). 눌림 시간이 보이지 않으면 고장으로 오해하므로,
    /// 꾹 누르는 동안 카드 묶음 아래에 작은 막대가 차오릅니다.
    /// 월드 고정(머리에 붙이지 않음, 두 눈 겹침 방지)이고 판 2장 + TMP 한 줄이라 가볍습니다.
    /// 앞면(-Z)이 플레이어 쪽인 회전을 받습니다(카드 묶음과 같은 회전).
    /// </summary>
    public sealed class SkipHoldGauge : MonoBehaviour
    {
        const float Width = 0.12f, Height = 0.01f;
        static readonly Color FillStart = new Color(1f, 0.86f, 0.4f);
        static readonly Color FillEnd = new Color(1f, 0.45f, 0.2f);

        SpriteRenderer fill;

        public static SkipHoldGauge Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("SkipHoldGauge");
            go.transform.SetParent(parent, false);
            var gauge = go.AddComponent<SkipHoldGauge>();

            RevealSprites.Quad(go.transform, "Back", RevealSprites.Center, Vector3.zero,
                new Vector2(Width + 0.004f, Height + 0.004f), new Color(0.08f, 0.06f, 0.12f, 0.85f), 0);
            // 채움은 왼쪽 끝 기준으로 늘어나고, 배경보다 0.6mm 플레이어 쪽(-Z)
            gauge.fill = RevealSprites.Quad(go.transform, "Fill", RevealSprites.Left, new Vector3(-Width * 0.5f, 0f, -0.0006f),
                new Vector2(0f, Height), FillStart, 1);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<TextMeshPro>();
            if (font != null) label.font = font;
            label.text = "<mark=#140F1ECC> 꾹 누르면 건너뛰기 · 유니크 이상은 보여 줘요 </mark>";
            label.fontSize = 0.09f; // 1 ≈ 10cm → 글자 약 9mm
            label.alignment = TextAlignmentOptions.Bottom;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var rt = (RectTransform)labelGo.transform;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0.3f, 0.03f);
            rt.localPosition = new Vector3(0f, Height * 0.5f + 0.006f, -0.0008f);

            go.SetActive(false);
            return gauge;
        }

        /// <param name="progress">0~1</param>
        public void Show(Pose pose, float progress)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            float p = Mathf.Clamp01(progress);
            fill.transform.localScale = new Vector3(Width * p, Height, 1f);
            fill.color = Color.Lerp(FillStart, FillEnd, p);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
