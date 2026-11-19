using TMPro;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 벽 · 받침대에 세워 두는 안내 판 하나 (환경 단서: 조작 안내 판, 알바 수칙 포스터 — 튜토리얼_설계 5-1 · 5-4).
    /// 바탕은 SpriteRenderer(코드로 만든 흰 스프라이트에 색)라 기본 스프라이트 머티리얼을 씁니다(CounterMemo와 같은 방식).
    /// 방향: 로컬 -Z 쪽이 읽는 면, 로컬 +Y가 글자 위쪽. 플레이어를 바라보게 세우려면 rotation = LookRotation(플레이어 → 판 방향).
    /// </summary>
    public sealed class WorldSign : MonoBehaviour
    {
        const float TextLift = 0.002f; // 글자를 바탕보다 읽는 쪽(-Z)으로 띄움

        static Sprite whiteSprite;

        /// <summary>판 글자 (내용을 바꾸는 판에서 씀: 가게 전광판)</summary>
        public TextMeshPro Text { get; private set; }

        /// <param name="size">판 크기 (m)</param>
        /// <param name="fontSize">TMP 3D 글자 크기 (메모 0.11, 말풍선 0.28 기준)</param>
        public static WorldSign Create(Transform parent, string name, TMP_FontAsset font, Pose pose, Vector2 size,
            Color background, Color ink, float fontSize, string message)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var sign = go.AddComponent<WorldSign>();

            var board = new GameObject("Board");
            board.transform.SetParent(go.transform, false);
            board.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = board.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite();
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
