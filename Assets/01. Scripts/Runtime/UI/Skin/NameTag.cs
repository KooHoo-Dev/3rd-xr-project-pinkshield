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
            // TMP 3D 글자는 fontSize 1 ≈ 0.1m 높이. 여백 · 모서리는 실제 글자 높이(em) 기준으로 잡아야 함
            // (11-20: fontSize를 그대로 곱해 판이 글자의 약 10배가 되어 말풍선 첫 줄을 가렸음)
            float em = fontSize * 0.1f;
            rt.sizeDelta = new Vector2(em * 20f, em * 1.6f);
            var pad = new Vector2(em * 0.6f, em * 0.25f);
            tag.inset = em * 1.2f;
            tag.plate = TextPlate.Attach(tag.text, Color.white, pad, em * 0.5f, default, 0.002f, false, sortBase);
            return tag;
        }

        public void Set(string label, Color fill)
        {
            text.text = label;
            text.color = UiTheme.Ink;
            plate.SetStyle(fill, default);
            plate.Fit();
        }

        /// <summary>판 영역(같은 부모 좌표)의 왼쪽 위 모서리에 걸치게 놓습니다. 이름표는 대부분 판 위로 올라가 글자를 가리지 않음</summary>
        public void PinTo(Rect area)
        {
            float rise = plate.Area.height * 0.25f; // 이름표 높이의 1/4만 판 안쪽으로 걸침
            transform.localPosition = new Vector3(area.xMin + inset, area.yMax + rise, -0.003f);
        }

        public void SetAlpha(float a)
        {
            text.alpha = a;
            plate.SetAlpha(a);
        }

        public void DrawOnTop() => plate.DrawOnTop();
    }
}
