using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardShop.UI
{
    /// <summary>
    /// 3D 글자(TextMeshPro) 뒤에 깔리는 둥근 판 (UI_개편_설계 C안). 예전 &lt;mark&gt; 검은 띠 대신 씁니다.
    /// 글자가 바뀔 때마다 <see cref="Fit"/>을 부르면 글자 크기에 맞춰 판이 늘어납니다 (9-slice라 모서리는 그대로).
    /// 바탕 · 테두리는 SpriteRenderer라 기본 스프라이트 머티리얼을 씁니다 (WorldSign · CounterMemo와 같은 방식).
    /// 순서: 바탕(sortBase) → 테두리(+1) → 글자(+2). 테두리는 실선 · 점선(<see cref="UiSprites.DashedFrame"/>) 중 고름.
    /// </summary>
    public sealed class TextPlate : MonoBehaviour
    {
        const float Lift = 0.0015f; // 판을 글자 뒤(+Z, 글자는 -Z 쪽에서 읽힘)로

        static Material overlay;

        TextMeshPro text;
        SpriteRenderer fill;
        SpriteRenderer frame;
        Vector2 padding;
        Color fillColor;
        Color frameColor;
        float ppu;
        float frameM;
        int sortBase;

        /// <summary>마지막 <see cref="Fit"/>에서 정한 판 영역 (글자 로컬 좌표, m). 이름표를 모서리에 붙일 때 씀</summary>
        public Rect Area { get; private set; }

        /// <param name="padding">글자 둘레 여백 (m)</param>
        /// <param name="cornerM">모서리 반지름 (m)</param>
        /// <param name="frameColor">테두리 색. 알파 0이면 테두리 없음</param>
        /// <param name="frameM">테두리 두께 (m)</param>
        /// <param name="dashed">테두리를 점선으로 (혼잣말)</param>
        /// <param name="sortBase">그리는 순서 시작 값. 다른 판 위에 겹치는 판(이름표 · 자막)은 크게</param>
        public static TextPlate Attach(TextMeshPro text, Color fill, Vector2 padding, float cornerM, Color frameColor = default, float frameM = 0.004f,
            bool dashed = false, int sortBase = 0)
        {
            UiFonts.Apply(text); // 글꼴 하나 · 모두 굵게
            var plate = text.GetComponent<TextPlate>();
            if (plate == null) plate = text.gameObject.AddComponent<TextPlate>();
            plate.text = text;
            plate.padding = padding;
            plate.frameM = frameM;
            plate.sortBase = sortBase;
            plate.ppu = UiSprites.Radius / Mathf.Max(0.001f, cornerM); // 모서리 32픽셀 = cornerM 미터
            plate.fill = plate.MakeRenderer("PlateFill", sortBase);
            plate.fill.sprite = UiSprites.Rounded(plate.ppu);
            plate.fill.drawMode = SpriteDrawMode.Sliced;
            plate.SetStyle(fill, frameColor, dashed);
            var mr = text.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = sortBase + 2;
            return plate;
        }

        /// <summary>바탕 · 테두리 색과 테두리 모양을 바꿉니다 (같은 말풍선이 사장님 ↔ 혼잣말로 바뀔 때). 다음 <see cref="Fit"/>에 크기가 맞춰짐</summary>
        public void SetStyle(Color fill, Color frameColor, bool dashed = false)
        {
            fillColor = fill;
            this.frameColor = frameColor;
            if (this.fill != null) this.fill.color = fill;
            if (frameColor.a <= 0f)
            {
                if (frame != null) frame.gameObject.SetActive(false);
                return;
            }
            if (frame == null) frame = MakeRenderer("PlateFrame", sortBase + 1);
            frame.gameObject.SetActive(true);
            int thicknessPx = Mathf.Clamp(Mathf.RoundToInt(frameM * ppu), 1, UiSprites.Radius - 2);
            frame.sprite = dashed ? UiSprites.DashedFrame(thicknessPx, ppu) : UiSprites.Frame(thicknessPx, ppu);
            frame.drawMode = dashed ? SpriteDrawMode.Tiled : SpriteDrawMode.Sliced; // 점선은 한 칸씩 반복해야 점이 늘어나지 않음
            frame.color = frameColor;
        }

        SpriteRenderer MakeRenderer(string name, int order)
        {
            Transform old = transform.Find(name);
            var go = old != null ? old.gameObject : new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            return sr;
        }

        /// <summary>지금 글자 크기에 판을 맞춥니다. 글자가 비면 판도 숨김</summary>
        public void Fit()
        {
            if (text == null || fill == null) return;
            text.ForceMeshUpdate(true); // 꺼져 있어도 (보이기 전에 크기를 맞춤)
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
            Area = new Rect((Vector2)center - size * 0.5f, size);
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

        /// <summary>
        /// 판과 글자를 물건에 가려지지 않게 맨 위에 그립니다 (튜토리얼 자막). 글자는 TMP 셰이더의 ZTest를 끄고,
        /// 판은 같은 ZTest 속성을 가진 UI/Default 셰이더로 바꿈 (스프라이트 기본 셰이더에는 ZTest 속성이 없음).
        /// </summary>
        public void DrawOnTop()
        {
            if (text != null)
            {
                Material m = text.fontMaterial;
                m.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
                m.renderQueue = (int)RenderQueue.Overlay;
            }
            Material sprites = OverlayMaterial();
            if (sprites == null) return;
            if (fill != null) fill.sharedMaterial = sprites;
            if (frame != null) frame.sharedMaterial = sprites;
        }

        static Material OverlayMaterial()
        {
            if (overlay != null) return overlay;
            Shader shader = Shader.Find("UI/Default"); // 기본 포함 셰이더 (Always Included)
            if (shader == null) return null;
            overlay = new Material(shader) { name = "TextPlateOverlay", hideFlags = HideFlags.DontSave, renderQueue = (int)RenderQueue.Overlay };
            overlay.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
            return overlay;
        }
    }
}
