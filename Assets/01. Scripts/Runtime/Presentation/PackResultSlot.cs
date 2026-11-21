using CardShop.Cards;
using CardShop.UI;
using TMPro;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 팩 결과판의 카드 한 칸 (UI 개편 샘플 ⑤ "이번 팩 결과"): 실제 카드 + 등급 색 테 + 아래 등급 칩(카드와 같은 슬라임 돔 마크 + 이름) + NEW 표.
    /// 등급별 반응 크기 차이(Pocket): 노말 · 레어는 테만, 수퍼레어부터 뒤에 빛, 유니크 이상은 빛이 더 큼.
    /// 눈 편한 색(VR): 빛은 블룸 없이 알파 0.3~0.45, 등급 이름 0.11(약 11mm) · 돔 마크 9mm, 테 3.5mm.
    /// 칸 원점 = 카드 가운데, 판 로컬 좌표(m). 카드는 앞면 -Z(보는 쪽)로 조금 띄워 판 위에 둠 (카드 셰이더가 불투명 · 깊이 씀이라 뒤 판이 가려짐).
    /// </summary>
    public sealed class PackResultSlot : MonoBehaviour
    {
        const float RimM = 0.0035f;     // 등급 색 테 두께
        const float RimGapM = 0.002f;   // 카드와 테 사이
        const float GlowM = 0.012f;     // 빛이 카드 밖으로 번지는 폭
        const float LiftM = 0.004f;     // 카드를 판 앞으로
        const float ChipFont = 0.11f;   // 등급 이름 (약 11mm)
        const float MarkM = 0.009f;     // 돔 마크 높이 (카드 위 마크를 키운 것)

        CardView card;
        SpriteRenderer glow, rim, fallback, badge;
        TextMeshPro chip, fallbackName, badgeText;
        RarityMarkRow marks;
        float cardW, cardH;

        public static PackResultSlot Create(Transform parent, float cardWidthM, float cardHeightM, int sortBase)
        {
            var go = new GameObject("Slot");
            go.transform.SetParent(parent, false);
            var slot = go.AddComponent<PackResultSlot>();
            slot.Build(cardWidthM, cardHeightM, sortBase);
            return slot;
        }

        /// <summary>칸 아래 등급 칩까지 포함한 높이 (판 배치용)</summary>
        public static float ChipHeightM => MarkM + 0.0025f + LabelH;
        static float LabelH => ChipFont * 0.1f * 1.45f;

        void Build(float w, float h, int sortBase)
        {
            cardW = w;
            cardH = h;
            float pad = RimGapM + RimM;
            glow = PackResultCards.MakeSprite(transform, "Glow", UiSprites.Glow(UiSprites.Radius / GlowM), Color.clear, sortBase,
                new Vector2(w + (pad + GlowM) * 2f, h + (pad + GlowM) * 2f), Vector3.zero);
            rim = PackResultCards.MakeSprite(transform, "Rim", UiSprites.Frame(Mathf.RoundToInt(UiSprites.Radius * RimM / 0.006f), UiSprites.Radius / 0.006f),
                Color.white, sortBase + 1, new Vector2(w + pad * 2f, h + pad * 2f), Vector3.zero);
            // 카드 프리팹이 없을 때(테스트 씬): 등급 색 칸 + 이름
            fallback = PackResultCards.MakeSprite(transform, "Fallback", UiSprites.Rounded(UiSprites.Radius / 0.004f), UiTheme.OsCard, sortBase + 1,
                new Vector2(w, h), Vector3.zero);
            fallbackName = PackResultCards.MakeText(fallback.transform, "Name", 0.1f, UiTheme.SoftText, sortBase + 2, new Vector2(w * 0.9f, h * 0.9f));
            fallbackName.textWrappingMode = TextWrappingModes.Normal;
            fallback.gameObject.SetActive(false);

            // 등급 칩: 위 = 카드와 같은 슬라임 돔 마크, 아래 = 등급 이름 (11-22 "카드 문양 기준", 예전 ◆ · ★ 글자 대신)
            float chipTop = -h * 0.5f - pad - 0.004f;
            marks = RarityMarkRow.Create(transform, MarkM, sortBase + 2);
            marks.transform.localPosition = new Vector3(0f, chipTop - MarkM * 0.5f, 0f);
            chip = PackResultCards.MakeText(transform, "Chip", ChipFont, Color.white, sortBase + 2, new Vector2(w + 0.01f, LabelH));
            chip.transform.localPosition = new Vector3(0f, chipTop - MarkM - 0.0025f - LabelH * 0.5f, 0f);
            chip.enableAutoSizing = true; // "레전더리"도 칸 폭 안에
            chip.fontSizeMin = ChipFont * 0.8f;
            chip.fontSizeMax = ChipFont;

            // NEW 표: 카드 오른쪽 위 모서리에 걸친 작은 초록 알약 (샘플: top -10px · right -8px)
            Vector2 badgeSize = new Vector2(0.028f, 0.014f);
            badge = PackResultCards.MakeSprite(transform, "New", UiSprites.Rounded(UiSprites.Radius / 0.004f), UiTheme.NewBadge, sortBase + 3, badgeSize,
                new Vector3(w * 0.5f - badgeSize.x * 0.25f, h * 0.5f, -LiftM - 0.002f));
            badgeText = PackResultCards.MakeText(badge.transform, "Label", 0.09f, UiTheme.SoftText, sortBase + 4, badgeSize);
            badgeText.text = "NEW";
        }

        /// <summary>카드 하나를 칸에 채웁니다. 카드 프리팹은 칸마다 하나를 만들어 다음 팩에 다시 씀</summary>
        public void Show(CardData data, bool isNew, CardRarityStyles styles)
        {
            CardRarity r = data != null ? data.rarity : CardRarity.Normal;
            Color c = styles != null ? styles.Get(r).frameColor : Color.white;

            rim.color = c;
            // 수퍼레어부터 뒤 빛 (유니크 이상은 더 진하게). 노말 · 레어는 조용히
            Color g = c;
            g.a = r >= CardRarity.Unique ? 0.45f : r == CardRarity.SuperRare ? 0.3f : 0f;
            glow.color = g;
            glow.enabled = g.a > 0f;

            string label = styles != null ? styles.Get(r).label : r.ToString();
            marks.Show(r);
            chip.text = label;
            chip.color = c;
            badge.gameObject.SetActive(isNew);

            if (card == null) card = DisplayCards.Create(transform, "Card");
            if (card != null)
            {
                card.gameObject.SetActive(data != null);
                if (data != null)
                {
                    card.Bind(data);
                    Transform t = card.transform;
                    t.localRotation = Quaternion.identity; // 카드 앞면 -Z = 판 글자와 같은 쪽
                    t.localScale = Vector3.one * (cardH / CardView.Height);
                    t.localPosition = new Vector3(0f, 0f, -LiftM);
                }
            }
            else
            {
                fallback.gameObject.SetActive(data != null);
                fallbackName.text = data != null ? data.displayName : "";
            }
        }

        /// <summary>나타나기 연출용 크기 (판이 뜬 뒤 칸마다 차례로 "톡"). 카드는 불투명 셰이더라 흐려지기 대신 크기로만 나타남</summary>
        public void SetPop(float scale) => transform.localScale = Vector3.one * scale;
    }
}
