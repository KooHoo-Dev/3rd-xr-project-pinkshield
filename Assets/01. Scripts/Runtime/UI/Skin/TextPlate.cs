using TMPro;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 3D 글자(TextMeshPro) 뒤에 깔리는 둥근 판 (UI_개편_설계 C안). 예전 &lt;mark&gt; 검은 띠 대신 씁니다.
    /// 글자가 바뀔 때마다 <see cref="Fit"/>을 부르면 글자 크기에 맞춰 판이 늘어납니다 (9-slice라 모서리는 그대로).
    /// 바탕 · 테두리는 SpriteRenderer라 기본 스프라이트 머티리얼을 씁니다 (WorldSign · CounterMemo와 같은 방식).
    /// 순서: 바탕(0) → 테두리(1) → 글자(2).
    /// </summary>
    public sealed class TextPlate : MonoBehaviour
    {
        const float Lift = 0.0015f; // 판을 글자 뒤(+Z, 글자는 -Z 쪽에서 읽힘)로

        TextMeshPro text;
        SpriteRenderer fill;
        SpriteRenderer frame;
        Vector2 padding;
        Color fillColor;
        Color frameColor;

        /// <param name="padding">글자 둘레 여백 (m)</param>
        /// <param name="cornerM">모서리 반지름 (m)</param>
        /// <param name="frameColor">테두리 색. 알파 0이면 테두리 없음</param>
        /// <param name="frameM">테두리 두께 (m)</param>
        public static TextPlate Attach(TextMeshPro text, Color fill, Vector2 padding, float cornerM, Color frameColor = default, float frameM = 0.004f)
        {
            UiFonts.Apply(text); // 글꼴 하나 · 모두 굵게
            var plate = text.GetComponent<TextPlate>();
            if (plate == null) plate = text.gameObject.AddComponent<TextPlate>();
            plate.text = text;
            plate.padding = padding;
            plate.fillColor = fill;
            plate.frameColor = frameColor;
            float ppu = UiSprites.Radius / Mathf.Max(0.001f, cornerM); // 모서리 32픽셀 = cornerM 미터
            plate.fill = plate.MakeRenderer("PlateFill", UiSprites.Rounded(ppu), fill, 0);
            if (frameColor.a > 0f)
            {
                int thicknessPx = Mathf.Clamp(Mathf.RoundToInt(frameM * ppu), 1, UiSprites.Radius - 2);
                plate.frame = plate.MakeRenderer("PlateFrame", UiSprites.Frame(thicknessPx, ppu), frameColor, 1);
            }
            var mr = text.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 2;
            return plate;
        }

        SpriteRenderer MakeRenderer(string name, Sprite sprite, Color color, int order)
        {
            Transform old = transform.Find(name);
            var go = old != null ? old.gameObject : new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.color = color;
            sr.sortingOrder = order;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return sr;
        }

        /// <summary>지금 글자 크기에 판을 맞춥니다. 글자가 비면 판도 숨김</summary>
        public void Fit()
        {
            if (text == null || fill == null) return;
            text.ForceMeshUpdate();
            Bounds b = text.textBounds;
            bool empty = string.IsNullOrEmpty(text.text) || b.size.x <= 0.0001f;
            fill.enabled = !empty;
            if (frame != null) frame.enabled = !empty;
            if (empty) return;

            Vector2 size = new Vector2(b.size.x, b.size.y) + padding * 2f;
            // 9-slice는 판이 모서리 두 개보다 작으면 깨지므로 최소 크기
            float min = fill.sprite.border.x * 2f / fill.sprite.pixelsPerUnit;
            size = Vector2.Max(size, new Vector2(min, min));
            Vector3 center = new Vector3(b.center.x, b.center.y, Lift);
            Place(fill, center, size);
            if (frame != null) Place(frame, center - new Vector3(0f, 0f, Lift * 0.5f), size);
        }

        static void Place(SpriteRenderer sr, Vector3 localCenter, Vector2 size)
        {
            sr.transform.localPosition = localCenter;
            sr.size = size;
        }

        /// <summary>글자와 같이 사라지게 (0~1)</summary>
        public void SetAlpha(float a)
        {
            if (fill != null) { Color c = fillColor; c.a *= a; fill.color = c; }
            if (frame != null) { Color c = frameColor; c.a *= a; frame.color = c; }
        }
    }
}
