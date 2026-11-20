using CardShop.UI;
using TMPro;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 벽 · 받침대에 세워 두는 안내 판 하나 (환경 단서: 조작 안내 판, 알바 수칙 포스터 — 튜토리얼_설계 5-1 · 5-4).
    /// 바탕은 SpriteRenderer(둥근 종이 스프라이트에 색)라 기본 스프라이트 머티리얼을 씁니다(CounterMemo와 같은 방식).
    /// UI 개편 C안: 종이 판 + 잉크 글자, 원하면 마스킹테이프 띠 · 기울임(<see cref="NoteTape"/>).
    /// 방향: 로컬 -Z 쪽이 읽는 면, 로컬 +Y가 글자 위쪽. 플레이어를 바라보게 세우려면 rotation = LookRotation(플레이어 → 판 방향).
    /// </summary>
    public sealed class WorldSign : MonoBehaviour
    {
        const float CornerM = 0.012f;  // 판 모서리 반지름 (m)
        const float TextLift = 0.002f; // 글자를 바탕보다 읽는 쪽(-Z)으로 띄움

        static Sprite whiteSprite;

        /// <summary>판 글자 (내용을 바꾸는 판에서 씀: 가게 전광판)</summary>
        public TextMeshPro Text { get; private set; }

        /// <param name="size">판 크기 (m)</param>
        /// <param name="fontSize">TMP 3D 글자 크기 (메모 0.11, 말풍선 0.28 기준)</param>
        public static WorldSign Create(Transform parent, string name, TMP_FontAsset font, Pose pose, Vector2 size,
            Color background, Color ink, float fontSize, string message, bool taped = false, float tiltDegrees = 0f,
            Color frame = default, float frameM = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var sign = go.AddComponent<WorldSign>();

            // 나무 틀 (칠판 판처럼 판보다 frameM만큼 큰 판을 뒤에): 타이틀 칠판과 같은 테마 (11-20)
            if (frame.a > 0f && frameM > 0f && Application.isPlaying)
            {
                var border = new GameObject("Frame");
                border.transform.SetParent(go.transform, false);
                border.transform.localPosition = new Vector3(0f, 0f, 0.002f);
                var fr = border.AddComponent<SpriteRenderer>();
                fr.sprite = UiSprites.Rounded(NoteTape.PpuFor(CornerM * 1.6f));
                fr.drawMode = SpriteDrawMode.Sliced;
                fr.size = size + Vector2.one * (frameM * 2f);
                fr.color = frame;
                fr.sortingOrder = -1;
            }

            var board = new GameObject("Board");
            board.transform.SetParent(go.transform, false);
            board.transform.localScale = new Vector3(size.x, size.y, 1f); // 둥근 판이면 아래에서 1로 되돌리고 size로 늘림
            var sr = board.AddComponent<SpriteRenderer>();
            sr.sprite = Application.isPlaying ? UiSprites.Rounded(NoteTape.PpuFor(CornerM)) : WhiteSprite();
            if (Application.isPlaying)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                board.transform.localScale = Vector3.one;
                sr.size = size;
            }
            sr.color = background;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -TextLift);
            var text = textGo.AddComponent<TextMeshPro>();
            if (font != null) text.font = font;
            text.fontSize = fontSize;
            text.color = ink;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.text = message;
            float margin = Mathf.Min(size.x, size.y) * 0.08f;
            ((RectTransform)textGo.transform).sizeDelta = size - new Vector2(margin * 2f, margin * 2f);
            text.GetComponent<MeshRenderer>().sortingOrder = 1;
            sign.Text = text;
            if (taped) NoteTape.Stick(go.transform, size);
            if (tiltDegrees != 0f) NoteTape.Tilt(go.transform, tiltDegrees);
            return sign;
        }

        static Sprite WhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            const int px = 4;
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[px * px];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px);
            return whiteSprite;
        }
    }
}
