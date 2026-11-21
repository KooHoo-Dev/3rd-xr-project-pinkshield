using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace CardShop.Cards
{
    /// <summary>
    /// CardData + 등급 스타일을 카드 프리팹의 셰이더와 텍스트에 적용합니다.
    /// 카드마다 머티리얼을 복제하지 않도록 MaterialPropertyBlock을 씁니다.
    /// </summary>
    [ExecuteAlways]
    public class CardView : MonoBehaviour
    {
        /// <summary>실제 TCG 카드 크기 63 × 88mm, 두께 1.2mm (프리팹 빌더와 연출이 같이 씀)</summary>
        public const float Width = 0.063f, Height = 0.088f, Thickness = 0.0012f;

        [SerializeField] CardData data;
        [SerializeField] CardRarityStyles styles;

        [Header("References")]
        [SerializeField] Renderer cardRenderer;
        [SerializeField] int frontMaterialIndex;
        [SerializeField] TMP_Text nameText;
        [Tooltip("오른쪽 위 속성 배지 아이콘 아틀라스 (가로 7칸: 불 · 물 · 땅 · 공기 · 무속성 · 필드 · 알)")]
        [SerializeField] Texture2D elementIcons;
        [SerializeField] TMP_Text descriptionText;
        [Tooltip("그림 창 아래 정보 띠: 도감 번호 · 속성 · 종류")]
        [SerializeField] TMP_Text infoText;
        [Tooltip("바닥 오른쪽 카드 ID (가격 자리였음. 등급은 셰이더가 마크로 그림)")]
        [FormerlySerializedAs("priceText")] [SerializeField] TMP_Text numberText;

        static readonly Color DarkText = new Color(0.16f, 0.12f, 0.2f);
        static readonly Color InfoDark = new Color(0.35f, 0.33f, 0.38f);
        static readonly Color InfoLight = new Color(1f, 0.91f, 0.72f);

        // 등급 마크 (카드 캔버스 v2): 노말 은색 1 · 레어 파랑 2 · 수퍼레어 보라 3 · 유니크 금색 1+반짝임 · 레전더리 무지개 1+반짝임
        static readonly int[] MarkCounts = { 1, 2, 3, 1, 1 };
        static readonly Color[] MarkColors =
        {
            new Color(0.66f, 0.68f, 0.72f), new Color(0.35f, 0.65f, 0.94f), new Color(0.65f, 0.48f, 0.94f),
            new Color(0.94f, 0.71f, 0.16f), Color.white,
        };

        static readonly int MarkCount = Shader.PropertyToID("_MarkCount");
        static readonly int MarkColor = Shader.PropertyToID("_MarkColor");
        static readonly int MarkRainbow = Shader.PropertyToID("_MarkRainbow");
        static readonly int MarkSparkle = Shader.PropertyToID("_MarkSparkle");
        static readonly int IconTex = Shader.PropertyToID("_IconTex");
        static readonly int IconIndex = Shader.PropertyToID("_IconIndex");
        static readonly int HasIcon = Shader.PropertyToID("_HasIcon");

        static readonly int CharTex = Shader.PropertyToID("_CharTex");
        static readonly int BgTex = Shader.PropertyToID("_BgTex");
        static readonly int HasChar = Shader.PropertyToID("_HasChar");
        static readonly int HasBg = Shader.PropertyToID("_HasBg");
        static readonly int BgAspect = Shader.PropertyToID("_BgAspect");
        static readonly int FrameColor = Shader.PropertyToID("_FrameColor");
        static readonly int AccentColor = Shader.PropertyToID("_AccentColor");
        static readonly int ElementColor = Shader.PropertyToID("_ElementColor");
        static readonly int FullArt = Shader.PropertyToID("_FullArt");
        static readonly int PopOut = Shader.PropertyToID("_PopOut");
        static readonly int CharScale = Shader.PropertyToID("_CharScale");
        static readonly int CharOffsetY = Shader.PropertyToID("_CharOffsetY");
        static readonly int FrameSparkle = Shader.PropertyToID("_FrameSparkle");
        static readonly int ArtHolo = Shader.PropertyToID("_ArtHolo");
        static readonly int Scroll = Shader.PropertyToID("_Scroll");
        static readonly int Rim = Shader.PropertyToID("_Rim");
        static readonly int Glow = Shader.PropertyToID("_Glow");

        MaterialPropertyBlock mpb;

        public CardData Data => data;
        public CardRarityStyles Styles => styles;

        public void Bind(CardData cardData)
        {
            data = cardData;
            Apply();
        }

        /// <summary>프리팹 빌더가 참조를 연결할 때 사용합니다.</summary>
        public void Configure(CardRarityStyles rarityStyles, Renderer renderer, int frontIndex,
            TMP_Text name, Texture2D icons, TMP_Text description, TMP_Text info, TMP_Text number)
        {
            styles = rarityStyles;
            cardRenderer = renderer;
            frontMaterialIndex = frontIndex;
            nameText = name;
            elementIcons = icons;
            descriptionText = description;
            infoText = info;
            numberText = number;
        }

        void OnEnable() => Apply();

#if UNITY_EDITOR
        void OnValidate()
        {
            // OnValidate 안에서 TMP 텍스트를 바꾸면 경고가 나므로 한 프레임 미룹니다.
            UnityEditor.EditorApplication.delayCall += () => { if (this) Apply(); };
        }
#endif

        public void Apply()
        {
            if (cardRenderer == null || styles == null) return;

            CardRarityStyles.Style s = styles.Get(data != null ? data.rarity : CardRarity.Normal);
            mpb ??= new MaterialPropertyBlock();
            cardRenderer.GetPropertyBlock(mpb, frontMaterialIndex);

            Texture2D character = data != null ? data.character : null;
            Texture2D background = data != null ? data.background : null;
            mpb.SetTexture(CharTex, character != null ? character : Texture2D.blackTexture);
            mpb.SetFloat(HasChar, character != null ? 1f : 0f);
            mpb.SetTexture(BgTex, background != null ? background : Texture2D.whiteTexture);
            mpb.SetFloat(HasBg, background != null ? 1f : 0f);
            mpb.SetFloat(BgAspect, background != null ? (float)background.width / background.height : 4f / 3f);

            mpb.SetColor(FrameColor, s.frameColor);
            mpb.SetColor(AccentColor, s.accentColor);
            mpb.SetColor(ElementColor, styles.GetElementColor(data != null ? data.element : CardElement.None));
            mpb.SetFloat(FullArt, s.fullArt ? 1f : 0f);
            mpb.SetFloat(PopOut, s.popOut ? 1f : 0f);
            mpb.SetFloat(CharScale, s.charScale);
            mpb.SetFloat(CharOffsetY, s.charOffsetY);
            mpb.SetFloat(FrameSparkle, s.frameSparkle);
            mpb.SetFloat(ArtHolo, s.artHolo);
            mpb.SetFloat(Scroll, s.scrollPattern);
            mpb.SetFloat(Rim, s.rim);
            mpb.SetFloat(Glow, s.glow);
            int r = Mathf.Clamp((int)(data != null ? data.rarity : CardRarity.Normal), 0, MarkCounts.Length - 1);
            mpb.SetFloat(MarkCount, MarkCounts[r]);
            mpb.SetColor(MarkColor, MarkColors[r]);
            mpb.SetFloat(MarkRainbow, r == (int)CardRarity.Legendary ? 1f : 0f);
            mpb.SetFloat(MarkSparkle, r >= (int)CardRarity.Unique ? 1f : 0f);
            mpb.SetTexture(IconTex, elementIcons != null ? elementIcons : Texture2D.blackTexture);
            mpb.SetFloat(HasIcon, elementIcons != null && data != null ? 1f : 0f);
            mpb.SetFloat(IconIndex, data != null ? IconSlot(data) : 0f);
            cardRenderer.SetPropertyBlock(mpb, frontMaterialIndex);

            Color textColor = s.fullArt ? Color.white : DarkText;
            SetText(nameText, data != null ? data.displayName : "", textColor);
            SetText(descriptionText, data != null ? data.description : "", textColor);
            SetText(infoText, data != null ? InfoLine(data) : "", s.fullArt ? InfoLight : InfoDark);
            SetText(numberText, data != null ? data.id : "", s.fullArt ? new Color(0.85f, 0.85f, 0.88f) : InfoDark);
        }

        /// <summary>속성 배지 아이콘 칸 (element_icons.png 순서). 필드 · 알 카드는 속성이 없어 종류 아이콘.</summary>
        public static int IconSlot(CardData card) => card.kind switch
        {
            CardKind.Field => 5,
            CardKind.Snack => 6,
            _ => card.element switch
            {
                CardElement.Fire => 0,
                CardElement.Water => 1,
                CardElement.Earth => 2,
                CardElement.Air => 3,
                _ => 4,
            },
        };

        static string ElementName(CardElement element) => element switch
        {
            CardElement.Fire => "불",
            CardElement.Water => "물",
            CardElement.Earth => "땅",
            CardElement.Air => "공기",
            _ => "무",
        };

        /// <summary>정보 띠 문구: "No.001 · 불 속성" (필드 · 알은 종류 이름)</summary>
        public static string InfoLine(CardData card)
        {
            string no = card.number > 0 ? $"No.{card.number:000} · " : "";
            return card.kind switch
            {
                CardKind.Field => no + "필드",
                CardKind.Snack => no + "슬라임 알",
                _ => no + (card.element == CardElement.Neutral ? "무속성" : ElementName(card.element) + " 속성"),
            };
        }

        static void SetText(TMP_Text text, string value, Color color)
        {
            if (text == null) return;
            text.text = value;
            text.color = color;
        }
    }
}
