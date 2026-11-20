using TMPro;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 말풍선 왼쪽 위 모서리에 걸치는 작은 이름표 ("사장님" · "혼잣말" · "손님"). 글자 + 색 칠한 둥근 판(TextPlate).
    /// 말풍선 판보다 앞에 그려야 하므로 그리는 순서를 말풍선보다 크게 줍니다.
    /// </summary>
    public sealed class NameTag : MonoBehaviour
    {
        TextMeshPro text;
        TextPlate plate;
        float inset;

        /// <param name="parent">말풍선 글자 (이름표 위치는 이 좌표계에서 정함)</param>
        /// <param name="fontSize">TMP 3D 글자 크기 (말풍선 글자의 약 70%)</param>
        /// <param name="sortBase">그리는 순서 시작 값 (말풍선 글자 순서 + 1 이상)</param>
        public static NameTag Create(Transform parent, float fontSize, int sortBase)
        {
            var go = new GameObject("NameTag", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tag = go.AddComponent<NameTag>();
            tag.text = go.AddComponent<TextMeshPro>();
            tag.text.fontSize = fontSize;
            tag.text.alignment = TextAlignmentOptions.MidlineLeft;
            tag.text.textWrappingMode = TextWrappingModes.NoWrap;
            tag.text.color = UiTheme.Ink;
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0f, 0.5f); // 왼쪽 끝이 원점 → 모서리에 맞추기 쉬움
            rt.sizeDelta = new Vector2(fontSize * 6f, fontSize * 1.4f);
            var pad = new Vector2(fontSize * 0.45f, fontSize * 0.18f);
            tag.inset = pad.x + fontSize * 0.6f;
            tag.plate = TextPlate.Attach(tag.text, Color.white, pad, fontSize * 0.3f, default, 0.004f, false, sortBase);
            return tag;
        }

        public void Set(string label, Color fill)
        {
            text.text = label;
            text.color = UiTheme.Ink;
            plate.SetStyle(fill, default);
            plate.Fit();
        }

        /// <summary>판 영역(같은 부모 좌표)의 왼쪽 위 모서리에 걸치게 놓습니다</summary>
        public void PinTo(Rect area)
        {
            transform.localPosition = new Vector3(area.xMin + inset, area.yMax, -0.003f);
        }

        public void SetAlpha(float a)
        {
            text.alpha = a;
            plate.SetAlpha(a);
        }

        public void DrawOnTop() => plate.DrawOnTop();
    }
}
