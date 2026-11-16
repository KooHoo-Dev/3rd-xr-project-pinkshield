using TMPro;
using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>
    /// CardData + 등급 스타일을 카드 프리팹의 셰이더와 텍스트에 적용합니다.
    /// 카드마다 머티리얼을 복제하지 않도록 MaterialPropertyBlock을 씁니다.
    /// </summary>
    [ExecuteAlways]
    public class CardView : MonoBehaviour
    {
        [SerializeField] CardData data;
        [SerializeField] CardRarityStyles styles;

        [Header("References")]
        [SerializeField] Renderer cardRenderer;
        [SerializeField] int frontMaterialIndex;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text rarityText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text priceText;

        static readonly Color DarkText = new Color(0.16f, 0.12f, 0.2f);

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
            TMP_Text name, TMP_Text rarity, TMP_Text description, TMP_Text price)
        {
            styles = rarityStyles;
            cardRenderer = renderer;
            frontMaterialIndex = frontIndex;
            nameText = name;
            rarityText = rarity;
            descriptionText = description;
            priceText = price;
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
            cardRenderer.SetPropertyBlock(mpb, frontMaterialIndex);

            Color textColor = s.fullArt ? Color.white : DarkText;
            SetText(nameText, data != null ? data.displayName : "", textColor);
            SetText(rarityText, s.label, s.fullArt ? s.accentColor : DarkText);
            SetText(descriptionText, data != null ? data.description : "", textColor);
            SetText(priceText, data != null ? $"가치 {data.basePrice}" : "", textColor);
        }

        static void SetText(TMP_Text text, string value, Color color)
        {
            if (text == null) return;
            text.text = value;
            text.color = color;
        }
    }
}
