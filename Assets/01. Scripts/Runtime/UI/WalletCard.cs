using CardShop.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardShop.UI
{
    /// <summary>
    /// 손목에 뜨는 SLIME PAY 카드의 모양 (UI 개편 C안 "손목 재화"). WristWallet이 자리 · 보임 · 돈 값을 정하고, 이 클래스는 그리기만 맡습니다.
    /// 진보라 둥근 카드 + 금색 얇은 테 + 왼쪽 위 "SLIME PAY" + 금색 IC칩 + 큰 돈 숫자.
    /// 돈이 바뀌면 <see cref="ShowDelta"/>로 "+N"(초록) · "−N"(빨강)이 위로 튀어 오르며 사라집니다 (unscaled time).
    /// 카드 중심이 원점, 글자는 −Z 쪽에서 읽힘 (TextPlate와 같은 방식).
    /// </summary>
    public sealed class WalletCard : MonoBehaviour
    {
        // 신용카드 비율(약 1.6:1). 크기는 예전 손목 글자판(0.15 × 0.07 m)과 비슷하게
        const float CardW = 0.14f, CardH = 0.088f;
        const float CornerM = 0.008f;   // 카드 모서리 반지름 (m)
        const float FrameM = 0.0015f;   // 금색 테 두께 (m)
        const float Lift = 0.002f;      // 카드 판을 글자 뒤(+Z)로
        const float DeltaSeconds = 1.3f;
        const float DeltaRise = 0.04f;  // 위로 튀어 오르는 높이 (m)

        // 어두운 카드 위 빨강: UiTheme.MoneyMinus는 밝은 종이용이라 어두운 판에서 흐려서, UiSkin.BrightGreen과 짝이 되는 밝은 빨강을 따로 둠
        static readonly Color BrightRed = new Color(1f, 0.55f, 0.55f);

        SpriteRenderer fill, frame, chip;
        TextMeshPro brand, amount, delta;
        Vector3 deltaHome;
        Color deltaColor;
        float alpha = 1f;
        float deltaStart = -999f;

        /// <summary>parent 아래에 카드를 만듭니다 (로컬 원점 중심)</summary>
        public static WalletCard Create(Transform parent)
        {
            var go = new GameObject("SlimePayCard");
            go.transform.SetParent(parent, false);
            var card = go.AddComponent<WalletCard>();
            card.Build();
            return card;
        }

        void Build()
        {
            float ppu = UiSprites.Radius / CornerM; // 모서리 32픽셀 = CornerM 미터
            var size = new Vector2(CardW, CardH);
            fill = MakeSprite("CardFill", UiSprites.Rounded(ppu), size, Vector2.zero, Lift, 0);
            int thickness = Mathf.Clamp(Mathf.RoundToInt(FrameM * ppu), 1, UiSprites.Radius - 2);
            frame = MakeSprite("CardFrame", UiSprites.Frame(thickness, ppu), size, Vector2.zero, Lift * 0.5f, 1);
            chip = MakeSprite("Chip", UiSprites.Rounded(UiSprites.Radius / 0.003f), new Vector2(0.024f, 0.018f), new Vector2(-0.048f, 0.004f), Lift * 0.25f, 2);

            brand = MakeText("Brand", "SLIME PAY", 0.1f, TextAlignmentOptions.Left, new Vector2(-0.025f, 0.029f), new Vector2(0.09f, 0.016f));
            amount = MakeText("Amount", "", 0.18f, TextAlignmentOptions.Right, new Vector2(0f, -0.02f), new Vector2(0.12f, 0.034f));
            amount.enableAutoSizing = true; // 큰 돈도 카드 밖으로 안 나가게
            amount.fontSizeMin = 0.08f;
            amount.fontSizeMax = 0.18f;
            deltaHome = new Vector3(0.055f, 0.012f, 0f);
            delta = MakeText("Delta", "", 0.14f, TextAlignmentOptions.Right, deltaHome, new Vector2(0.07f, 0.02f));
            delta.enabled = false;
            Recolor();
        }

        SpriteRenderer MakeSprite(string name, Sprite sprite, Vector2 size, Vector2 pos, float z, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, z);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.sortingOrder = order;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            return sr;
        }

        TextMeshPro MakeText(string name, string content, float fontSize, TextAlignmentOptions align, Vector2 pos, Vector2 rect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            ((RectTransform)go.transform).sizeDelta = rect;
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMeshPro>();
            t.font = WorldUiKit.KoreanFont;
            UiFonts.Apply(t);
            t.fontSize = fontSize;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.text = content;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 3;
            return t;
        }

        /// <summary>큰 돈 숫자 글자 (이미 "$1,250" 꼴로 만든 것)</summary>
        public void SetAmount(string formatted)
        {
            if (amount != null && amount.text != formatted) amount.text = formatted;
        }

        /// <summary>카드 전체 투명도 (0이면 그리지 않음)</summary>
        public void SetAlpha(float a)
        {
            alpha = Mathf.Clamp01(a);
            if (gameObject.activeSelf != alpha > 0.001f) gameObject.SetActive(alpha > 0.001f);
            if (alpha > 0.001f) Recolor();
        }

        /// <summary>바뀐 만큼을 띄웁니다: 늘면 초록 "+N", 줄면 빨강 "−N"</summary>
        public void ShowDelta(int change)
        {
            if (change == 0 || delta == null) return;
            delta.text = MoneyText.Signed(change);
            deltaColor = change > 0 ? UiSkin.BrightGreen : BrightRed;
            deltaStart = Time.unscaledTime;
            delta.enabled = true;
        }

        void LateUpdate()
        {
            if (delta == null || !delta.enabled) return;
            float t = (Time.unscaledTime - deltaStart) / DeltaSeconds;
            if (t >= 1f) { delta.enabled = false; return; }
            float rise = 1f - (1f - t) * (1f - t); // 빨리 튀어 올라 천천히 멈춤
            delta.transform.localPosition = deltaHome + new Vector3(0f, DeltaRise * rise, 0f);
            Recolor();
        }

        void Recolor()
        {
            Tint(fill, UiTheme.CardBack);
            Tint(frame, UiTheme.Gold);
            Tint(chip, UiTheme.Gold);
            Tint(brand, UiTheme.Gold);
            Tint(amount, UiTheme.Text);
            if (delta != null && delta.enabled)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - deltaStart) / DeltaSeconds);
                float fade = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f; // 뒤쪽 40%에서 흐려짐
                Tint(delta, deltaColor, fade);
            }
        }

        void Tint(SpriteRenderer sr, Color c)
        {
            if (sr == null) return;
            c.a *= alpha;
            sr.color = c;
        }

        void Tint(TMP_Text t, Color c, float extra = 1f)
        {
            if (t == null) return;
            c.a *= alpha * extra;
            t.color = c;
        }
    }
}
