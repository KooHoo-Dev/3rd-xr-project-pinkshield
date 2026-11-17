using System.Collections;
using CardShop.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.Presentation
{
    /// <summary>
    /// 바인더 한 칸 (월드 UI). 페이지를 넘기면 같은 칸 오브젝트에 다른 카드를 다시 그립니다(오브젝트 재사용).
    /// 발견한 카드 = 그림 + 이름 + 보유 장수, 못 얻은 카드 = 검은 실루엣 + 번호, 팔아서 0장 = "판매됨"(설계서 Q9).
    /// </summary>
    public sealed class BinderSlotView : MonoBehaviour
    {
        static readonly Color UnknownFrame = new Color(0.32f, 0.3f, 0.36f);
        static readonly Color UnknownBg = new Color(0.16f, 0.15f, 0.2f);
        static readonly Color Silhouette = new Color(0f, 0f, 0f, 0.85f);
        static readonly Color HighlightColor = new Color(1f, 0.85f, 0.25f, 1f);
        static readonly Color DarkText = new Color(0.16f, 0.12f, 0.2f);
        static readonly Color LightText = new Color(0.75f, 0.73f, 0.8f);

        [SerializeField] Button button;
        [SerializeField] Image frame;
        [SerializeField] Image artBackground;
        [SerializeField] RawImage art;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text countText;
        [Tooltip("선택·방금 넣은 카드 테두리 (칸 뒤에 조금 크게 깔린 이미지)")]
        [SerializeField] Image highlight;
        [SerializeField] float flashSeconds = 0.6f;

        bool selected;
        Coroutine flash;

        public Button Button => button;

        /// <summary>씬 빌더가 참조를 연결할 때 사용합니다.</summary>
        public void Configure(Button slotButton, Image frameImage, Image artBg, RawImage artImage, TMP_Text name, TMP_Text count, Image highlightImage)
        {
            button = slotButton;
            frame = frameImage;
            artBackground = artBg;
            art = artImage;
            nameText = name;
            countText = count;
            highlight = highlightImage;
        }

        /// <param name="card">null이면 빈 칸(마지막 페이지의 남는 칸)이라 숨깁니다.</param>
        /// <param name="number">바인더 전체 번호 (1부터)</param>
        public void Show(CardData card, int number, bool discovered, int count, CardRarityStyles styles)
        {
            if (card == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);

            art.texture = card.character;
            art.enabled = card.character != null;

            if (discovered)
            {
                frame.color = styles != null ? styles.Get(card.rarity).frameColor : Color.white;
                artBackground.color = styles != null ? styles.GetElementColor(card.element) : Color.gray;
                art.color = Color.white;
                nameText.text = card.displayName;
                nameText.color = DarkText;
                // 배경판 없이도 읽히도록 글자 뒤에 어두운 띠
                countText.text = count > 0 ? $"<mark=#140F1ECC> x{count} </mark>" : "<mark=#140F1ECC> 판매됨 </mark>";
            }
            else
            {
                frame.color = UnknownFrame;
                artBackground.color = UnknownBg;
                art.color = Silhouette;
                nameText.text = $"No.{number}  ???";
                nameText.color = LightText;
                countText.text = "";
            }
            // 못 얻은 카드는 고를 수 없음 (선택 이벤트는 발견한 카드만)
            button.interactable = discovered;
        }

        public void SetSelected(bool on)
        {
            selected = on;
            if (flash == null) ApplyHighlight(on ? 1f : 0f);
        }

        /// <summary>카드가 막 들어왔을 때 테두리를 한 번 반짝입니다.</summary>
        public void Flash()
        {
            if (!isActiveAndEnabled) return;
            if (flash != null) StopCoroutine(flash);
            flash = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / flashSeconds)
            {
                ApplyHighlight(Mathf.Lerp(1f, selected ? 1f : 0f, t) * (0.6f + 0.4f * Mathf.Cos(t * Mathf.PI * 4f)));
                yield return null;
            }
            flash = null;
            ApplyHighlight(selected ? 1f : 0f);
        }

        void OnDisable()
        {
            flash = null; // 비활성화되면 코루틴은 멈추므로 상태만 정리
            ApplyHighlight(selected ? 1f : 0f);
        }

        void ApplyHighlight(float alpha)
        {
            if (highlight == null) return;
            highlight.enabled = alpha > 0.001f;
            Color c = HighlightColor;
            c.a = alpha;
            highlight.color = c;
        }
    }
}
