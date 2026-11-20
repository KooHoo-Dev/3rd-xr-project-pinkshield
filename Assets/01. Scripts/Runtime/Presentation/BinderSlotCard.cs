using CardShop.Cards;
using CardShop.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.Presentation
{
    /// <summary>
    /// 바인더 칸 안의 실제 카드 (UI 개편 11-20: "카드 바인더는 카드 원형 그대로").
    /// 가진 카드 = 손에 드는 카드와 같은 프리팹을 투명 슬리브(흰 테) 안에 꽂고, 상태(장수 · 판매 등록 · 진열)는 카드 아래 꼬리표.
    /// 없는 카드 = 카드 모양 점선 + 큰 "?" + 번호. 칸 오브젝트처럼 카드도 한 칸에 하나만 만들어 쪽을 넘길 때 다시 씀 (Quest 성능).
    /// 레이 · 선택은 칸의 Frame(투명)이 그대로 받음. 실행 중에만 동작 (에디터에서 씬을 열어도 카드를 만들지 않음).
    /// </summary>
    public sealed class BinderSlotCard : MonoBehaviour
    {
        const float LabelMm = 30f;   // 아래 꼬리표 줄 높이
        const float SleeveMm = 5f;   // 슬리브가 카드보다 큰 만큼

        RectTransform slot;
        CardView card;
        Image sleeve, ghost;
        TMP_Text ghostMark;

        /// <summary>칸 안에서 카드가 놓일 자리 (mm, 칸 가운데 기준)와 높이</summary>
        void Layout(out float heightMm, out Vector2 centerMm, out float widthMm)
        {
            Rect r = slot.rect;
            heightMm = Mathf.Max(10f, r.height - LabelMm - SleeveMm * 2f);
            widthMm = heightMm * (CardView.Width / CardView.Height);
            if (widthMm > r.width - SleeveMm * 2f) { widthMm = r.width - SleeveMm * 2f; heightMm = widthMm * (CardView.Height / CardView.Width); }
            centerMm = new Vector2(0f, LabelMm * 0.5f);
        }

        public void Show(CardData data, bool discovered)
        {
            if (!Application.isPlaying) return;
            if (slot == null) Build();
            Layout(out float h, out Vector2 c, out float w);

            bool hasCard = discovered && data != null;
            if (hasCard && card == null) card = DisplayCards.Create(transform, "SlotCard");
            if (card != null)
            {
                card.gameObject.SetActive(hasCard);
                if (hasCard) { card.Bind(data); DisplayCards.Fit(card, h, c); }
            }
            Place(sleeve, w + SleeveMm * 2f, h + SleeveMm * 2f, c);
            sleeve.gameObject.SetActive(hasCard);
            Place(ghost, w, h, c);
            ghost.gameObject.SetActive(!hasCard);
        }

        void Build()
        {
            slot = (RectTransform)transform;
            // 투명 슬리브: 옅은 흰 판 + 흰 테 (카드 뒤)
            sleeve = WorldUiKit.AddImage(transform, "Sleeve", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(1f, 1f, 1f, 0.08f));
            sleeve.raycastTarget = false;
            sleeve.sprite = UiSprites.Rounded();
            sleeve.type = Image.Type.Sliced;
            sleeve.pixelsPerUnitMultiplier = UiSprites.Radius / 8f;
            Image rim = WorldUiKit.AddImage(sleeve.transform, "Rim", Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.35f));
            rim.raycastTarget = false;
            rim.sprite = UiSprites.Frame(4);
            rim.type = Image.Type.Sliced;
            rim.pixelsPerUnitMultiplier = UiSprites.Radius / 8f;
            sleeve.transform.SetSiblingIndex(1); // 칸 Frame 바로 위, 글자 아래

            // 없는 카드: 카드 모양 점선 + "?"
            ghost = WorldUiKit.AddImage(transform, "Ghost", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.37f, 0.408f, 0.565f)); // #5E6890
            ghost.raycastTarget = false;
            ghost.sprite = UiSprites.DashedFrame(5);
            ghost.type = Image.Type.Tiled;
            ghost.pixelsPerUnitMultiplier = UiSprites.Radius / 8f;
            ghostMark = WorldUiKit.AddText(ghost.transform, "Mark", "?", Vector2.zero, Vector2.one, 70f, new Color(0.486f, 0.525f, 0.682f), UiFonts.Main); // #7C86AE
            ghostMark.alignment = TextAlignmentOptions.Center;
        }

        static void Place(Image image, float wMm, float hMm, Vector2 centerMm)
        {
            var rt = (RectTransform)image.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(wMm, hMm);
            rt.anchoredPosition = centerMm;
        }
    }
}
