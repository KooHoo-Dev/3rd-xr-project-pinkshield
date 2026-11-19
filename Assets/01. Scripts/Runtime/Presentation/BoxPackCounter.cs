using CardShop.UI;
using TMPro;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 상자 카드를 넘기는 동안 카드 묶음 위에 "3 / 10 팩" (빌드피드백_11-19_FIX설계 B4). 새 팩으로 넘어가면 숫자가 톡 커졌다 돌아옵니다.
    /// CardRevealer는 넘길 카드가 몇 번째 팩인지만 알려 줍니다.
    /// </summary>
    public sealed class BoxPackCounter : MonoBehaviour
    {
        const float Above = 0.1f;        // 카드 묶음 가운데에서 위로 (m)
        const float PopSeconds = 0.2f;

        TextMeshPro text;
        int shown = -1;
        float popAt = -1f;

        public static BoxPackCounter Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("BoxPackCounter", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<BoxPackCounter>();
            c.text = go.AddComponent<TextMeshPro>();
            if (font != null) c.text.font = font;
            c.text.fontSize = 0.3f;
            c.text.alignment = TextAlignmentOptions.Center;
            c.text.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)go.transform).sizeDelta = new Vector2(0.3f, 0.06f);
            go.SetActive(false);
            return c;
        }

        /// <param name="pack">지금 넘기는 카드의 팩 번호 (1부터)</param>
        /// <param name="stack">카드 묶음 자세 (앞 = 머리에서 멀어지는 쪽)</param>
        public void Show(int pack, int total, Pose stack)
        {
            transform.SetPositionAndRotation(stack.position + stack.rotation * Vector3.up * Above, stack.rotation);
            gameObject.SetActive(true);
            if (pack == shown) return;
            shown = pack;
            popAt = Time.unscaledTime;
            text.text = $"<mark={UiTheme.MarkStrong} padding=\"10,10,4,4\"><b>{pack}</b> / {total} 팩</mark>";
        }

        public void Hide()
        {
            shown = -1;
            gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            float t = popAt < 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - popAt) / PopSeconds);
            transform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, t);
        }
    }
}
