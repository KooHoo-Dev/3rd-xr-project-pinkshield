using TMPro;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 카운터 위 메모 쪽지 (환경 단서: 언제든 다시 볼 수 있는 그날의 할 일). 문구가 빈 날은 숨깁니다.
    /// 종이는 SpriteRenderer(코드로 만든 흰 스프라이트에 연노랑 색)라 기본 스프라이트 머티리얼을 그대로 씁니다.
    /// 방향: 로컬 -Z 쪽이 읽는 면(TMP 앞면), 로컬 +Y가 글자 위쪽입니다.
    /// 지금은 카운터에 60° 세운 메모판 (FIX B6: 눕힌 종이는 잘 안 읽힘). 글자는 1.5배, 넘치면 자동으로 줄어들어 잘리지 않음.
    /// </summary>
    public sealed class CounterMemo : MonoBehaviour
    {
        static readonly Vector2 PaperSize = new Vector2(0.28f, 0.17f);
        static readonly Color PaperColor = new Color(1f, 0.96f, 0.72f);
        static readonly Color InkColor = new Color(0.2f, 0.17f, 0.25f);
        const float FontSize = 0.15f;   // TMP 3D 단위 (예전 0.1의 1.5배)
        const float MinFontSize = 0.09f; // 문구가 길면 여기까지 줄임
        const float Margin = 0.012f;    // 종이 가장자리 여백 (m)
        const float TextLift = 0.001f;  // 글자를 종이보다 읽는 쪽(-Z)으로 띄움 (겹침 깜빡임 방지)

        static Sprite whiteSprite;

        TextMeshPro text;
        GameObject paper;

        /// <summary>parent 아래에 메모를 만듭니다 (처음에는 숨김).</summary>
        public static CounterMemo Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("CounterMemo");
            go.transform.SetParent(parent, false);
            var memo = go.AddComponent<CounterMemo>();

            memo.paper = new GameObject("Paper");
            memo.paper.transform.SetParent(go.transform, false);
            memo.paper.transform.localScale = new Vector3(PaperSize.x, PaperSize.y, 1f);
            var sr = memo.paper.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite();
            sr.color = PaperColor;
            sr.sortingOrder = 0;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -TextLift);
            memo.text = textGo.AddComponent<TextMeshPro>();
            if (font != null) memo.text.font = font;
            memo.text.fontSize = FontSize;
            memo.text.color = InkColor;
            memo.text.alignment = TextAlignmentOptions.TopLeft;
            memo.text.textWrappingMode = TextWrappingModes.Normal;
            memo.text.enableAutoSizing = true;
            memo.text.fontSizeMin = MinFontSize;
            memo.text.fontSizeMax = FontSize;
            memo.text.overflowMode = TextOverflowModes.Truncate;
            ((RectTransform)textGo.transform).sizeDelta = PaperSize - new Vector2(Margin * 2f, Margin * 2f);
            memo.text.GetComponent<MeshRenderer>().sortingOrder = 1;

            go.SetActive(false);
            return memo;
        }

        /// <summary>메모 자리. 로컬 -Z가 읽는 사람 쪽을 향하게 회전을 주세요.</summary>
        public void Place(Pose pose) => transform.SetPositionAndRotation(pose.position, pose.rotation);

        /// <summary>문구를 바꿔 보여 줍니다. 서명("— 사장")은 문구에 포함해 주세요. 비어 있으면 숨깁니다.</summary>
        public void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) { Hide(); return; }
            text.text = message;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        // 1 × 1 m 흰 스프라이트 (모든 메모가 함께 씀)
        static Sprite WhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            const int size = 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return whiteSprite;
        }
    }
}
