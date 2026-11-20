using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using CardShop.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 모니터 세 번째 쪽 [판매 · 진열] (집_책상_하나로_설계 3-2, DEC-042).
    /// 바인더 양옆에 있던 판매 판(SalesPanel, 씬) · 진열 판(ShowcasePanel, 런타임)을 실행할 때 모니터 앞으로 옮겨 좌우로 놓고,
    /// 위에 "내일 인기 속성 · 고른 카드" 한 줄을 둡니다 — 커뮤니티 쪽을 오가지 않고 판매를 정하게.
    /// 바인더에서 카드를 고르면 모니터가 이 쪽으로 넘어갑니다 (Q5). 판 두 장의 버튼 · 규칙 코드는 그대로이고 부모 · 크기만 바뀝니다.
    /// </summary>
    public sealed class SalesShowcasePage : HomePanel
    {
        // 창 자리(HomeComputer.WindowW × WindowH) 안에 위 알림 띠 + SlimeOS 창 두 장 (판매 440 × 820, 진열 440 × 760)
        const float HeaderMm = 64f;      // 위 알림 띠 높이
        const float HeaderGapMm = 12f;
        const float PanelGapMm = 30f;    // 두 창 사이
        const float PanelWidthMm = 440f, SalesHeightMm = 820f;
        const string PageName = "판매 · 진열";

        HomeComputer computer;
        BinderView binder;
        TMP_Text header;
        CardView thumb;          // 판매 창 오른쪽 위: 고른 카드 그대로 (바인더와 같은 카드 프리팹)
        RectTransform thumbArea;
        GameObject priceTag;     // 노란 꼬리표 가격표 (샘플 6-1)
        TMP_Text priceText;
        int pageIndex = -1;

        static float PanelScale => (HomeComputer.WindowH - HeaderMm - HeaderGapMm) / SalesHeightMm;

        /// <summary>판매 · 진열 판을 모니터 쪽으로 옮겨 붙입니다. 판매 판이 없으면 아무것도 하지 않음</summary>
        public static SalesShowcasePage Create(HomeComputer computer, BinderView binder, ShowcasePanel showcase, TMP_FontAsset font)
        {
            SalesPanel sales = FindAnyObjectByType<SalesPanel>(FindObjectsInactive.Include);
            if (computer == null || binder == null || sales == null)
            {
                Debug.LogWarning("[SalesShowcasePage] 모니터 · 바인더 · 판매 판 중 하나가 없어 판매 · 진열 쪽을 만들지 않았습니다 (판은 바인더 옆에 그대로).");
                return null;
            }

            Transform parent = computer.ScreenParent;
            Quaternion rot = computer.ScreenRotation;

            // 바탕: 위 알림 띠(내일 인기 · 고른 카드) + 빈 곳 끌기 판 (창 두 장보다 뒤)
            RectTransform back = computer.CreateWindowCanvas("SalesShowcasePage", 0.008f, out GameObject content);
            back.gameObject.SetActive(false); // 설정을 다 넣은 뒤 켬 (OnEnable 순서)
            WorldUiKit.AddImage(content.transform, "SwipeHit", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            float headerMin = 1f - HeaderMm / HomeComputer.WindowH;
            Image strip = WorldUiKit.AddImage(content.transform, "HeaderStrip", new Vector2(0.02f, headerMin), new Vector2(0.98f, 1f), UiTheme.OsTaskbar);
            strip.raycastTarget = false;
            if (Application.isPlaying)
            {
                strip.sprite = UiSprites.Rounded();
                strip.type = Image.Type.Sliced;
                strip.pixelsPerUnitMultiplier = UiSprites.Radius / 12f;
            }
            TMP_Text text = WorldUiKit.AddText(content.transform, "Header", "", new Vector2(0.04f, headerMin), new Vector2(0.96f, 1f), 26f, UiTheme.SoftText, font);
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = 26f;
            computer.AddSwipe(back);

            // 창 두 장: 알림 띠 아래에 위끝을 맞춰 좌우로
            Vector3 center = computer.WindowCenter(0.012f);
            float panelTop = (HomeComputer.WindowH * 0.5f - HeaderMm - HeaderGapMm) * 0.001f;
            float half = (PanelWidthMm * PanelScale + PanelGapMm) * 0.0005f;
            var salesRt = (RectTransform)sales.transform;
            Place(salesRt, parent, center + rot * new Vector3(-half, 0f, 0f), rot, panelTop);
            Dress(salesRt, "판매  <size=75%>가게 판매대 · 매입</size>", font);
            StyleByName(salesRt, "Button_판매 등록", UiButtonKind.Primary, exact: true); // 목록 버튼 "판매 등록 1…"은 빼고
            StyleByName(salesRt, "즉시 판매", UiButtonKind.Paper);
            StyleByName(salesRt, "모두 팔기", UiButtonKind.Paper); // 샘플 6-1: 아래 수수한 버튼 (한 번 더 눌러야 팔리는 확인은 그대로)
            RectTransform showcaseRt = showcase != null ? (RectTransform)showcase.transform : null;
            if (showcaseRt != null)
            {
                Place(showcaseRt, parent, center + rot * new Vector3(half, 0f, 0f), rot, panelTop);
                Dress(showcaseRt, "내 방 진열장", font);
                StyleByName(showcaseRt, "Button_진열장에 세우기", UiButtonKind.Primary, exact: true);
            }

            var page = back.gameObject.AddComponent<SalesShowcasePage>();
            page.BuildThumb(salesRt.Find("Content"), font);
            page.content = content;
            page.computer = computer;
            page.binder = binder;
            page.header = text;
            page.pageIndex = computer.AddPage(new CanvasPage(back, salesRt, showcaseRt), PageName);
            back.gameObject.SetActive(true);
            return page;
        }

        // 판을 모니터 부모로 옮기고, 위끝을 top(창 가운데 기준 m)에 맞춤
        static void Place(RectTransform rt, Transform parent, Vector3 pos, Quaternion rot, float top)
        {
            rt.SetParent(parent, false);
            rt.localRotation = rot;
            rt.localScale = Vector3.one * (0.001f * PanelScale);
            float h = rt.sizeDelta.y * 0.001f * PanelScale;
            rt.localPosition = pos + rot * new Vector3(0f, top - h * 0.5f, 0f);
        }

        // 판(캔버스 아래 Content)을 SlimeOS 창으로
        static void Dress(RectTransform canvas, string title, TMP_FontAsset font)
        {
            Transform panel = canvas.Find("Content");
            if (panel != null) SlimeOsWindow.Apply(panel, title, font);
        }

        // 버튼 이름(WorldUiKit = "Button_" + 처음 글자)에 word가 들어간 버튼을 칠함
        static void StyleByName(Transform root, string word, UiButtonKind kind, bool exact = false)
        {
            foreach (Button b in root.GetComponentsInChildren<Button>(true))
                if (exact ? b.name == word : b.name.Contains(word)) UiSkin.StyleButton(b, kind);
        }

        // 판매 창 "Selected" 글자를 왼쪽으로 좁히고, 오른쪽에 고른 카드 + 노란 꼬리표 가격표
        void BuildThumb(Transform panel, TMP_FontAsset font)
        {
            if (panel == null || !Application.isPlaying) return;
            Transform selected = panel.Find("Selected");
            if (selected is RectTransform srt) srt.anchorMax = new Vector2(0.68f, srt.anchorMax.y);
            thumbArea = (RectTransform)WorldUiKit.AddImage(panel, "Thumb", new Vector2(0.72f, 0.775f), new Vector2(0.95f, 0.94f), new Color(0f, 0f, 0f, 0f)).transform;
            thumbArea.GetComponent<Image>().raycastTarget = false;

            Image tag = WorldUiKit.AddImage(panel, "PriceTag", new Vector2(0.66f, 0.735f), new Vector2(0.96f, 0.785f), UiTheme.PriceTag);
            tag.raycastTarget = false;
            tag.sprite = UiSprites.Rounded();
            tag.type = Image.Type.Sliced;
            tag.pixelsPerUnitMultiplier = UiSprites.Radius / 6f;
            var hole = WorldUiKit.AddImage(tag.transform, "Hole", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), UiTheme.Ink);
            hole.raycastTarget = false;
            hole.sprite = UiSprites.Circle();
            ((RectTransform)hole.transform).sizeDelta = new Vector2(9f, 9f);
            ((RectTransform)hole.transform).anchoredPosition = new Vector2(12f, 0f);
            priceText = WorldUiKit.AddText(tag.transform, "Price", "", new Vector2(0.15f, 0f), new Vector2(0.97f, 1f), 24f, UiTheme.Ink, font);
            priceText.alignment = TextAlignmentOptions.Center;
            priceText.enableAutoSizing = true;
            priceText.fontSizeMin = 14f;
            priceText.fontSizeMax = 24f;
            priceTag = tag.gameObject;
            priceTag.SetActive(false);
        }

        void RedrawThumb(CardData card, GameContext ctx)
        {
            if (thumbArea == null) return;
            if (card != null && thumb == null) thumb = DisplayCards.Create(thumbArea, "SalesThumb");
            if (thumb != null)
            {
                thumb.gameObject.SetActive(card != null);
                if (card != null)
                {
                    thumb.Bind(card);
                    DisplayCards.Fit(thumb, thumbArea.rect.height, Vector2.zero, 3f);
                }
            }
            int price = card != null ? ctx.Sales.InstantPrice(card, ctx.Session) : 0;
            priceTag.SetActive(card != null);
            if (card != null) priceText.text = price > 0 ? $"매입 {MoneyText.Format(price)}" : "묶음 매입";
        }

        // 카드는 3D 물건이라 CanvasGroup 투명도로 숨지 않음 → 이 쪽이 보일 때만 켬
        void Update()
        {
            if (thumb == null || computer == null) return;
            bool show = IsVisible && computer.CurrentPage == pageIndex && binder != null && binder.SelectedCard != null;
            if (thumb.gameObject.activeSelf != show) thumb.gameObject.SetActive(show);
        }

        protected override void OnEnable()
        {
            if (binder == null) return; // Create가 설정을 넣기 전
            binder.CardSelected += OnCardSelected;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            if (binder == null) return;
            binder.CardSelected -= OnCardSelected;
            base.OnDisable();
        }

        // 바인더에서 카드를 고르면 이 쪽으로 넘어감 (Q5). 집에서만
        void OnCardSelected(CardData _)
        {
            MarkDirty();
            if (computer != null && pageIndex >= 0 && computer.IsHome) computer.GoTo(pageIndex);
        }

        protected override void Redraw()
        {
            GameContext ctx = Context;
            if (header == null || ctx == null || ctx.Session == null || ctx.Market == null) return;

            int day = ctx.Session.Day;
            int dayCount = DayRules.EffectiveDayCount(ctx.Campaign.DayCount, ctx.Session.Endless.Active);
            bool hasTomorrow = day < dayCount;
            MarketDay tomorrow = hasTomorrow ? ctx.Market.For(day + 1) : MarketDay.None;

            string line = !hasTomorrow ? "내일 예보 없음 (마지막 날)"
                : !tomorrow.HasPopular ? "내일 인기 속성 없음"
                : $"내일 인기 <color=#7FD1FF>★ {CommunityBoard.ElementName(tomorrow.Primary)}</color> · 2순위 {CommunityBoard.ElementName(tomorrow.Secondary)}";

            CardData card = binder != null ? binder.SelectedCard : null;
            RedrawThumb(card, ctx);
            if (card != null)
            {
                int rank = tomorrow.RankOf(card.element);
                string star = rank == 1 ? "  <color=#FFC857>★ 내일 1순위!</color>" : rank == 2 ? "  <color=#FFB07A>★ 내일 2순위</color>" : "";
                line += $"   |   고른 카드  {card.displayName}{star}";
            }
            else line += "   |   바인더에서 카드를 골라 주세요";
            header.text = line;
        }
    }
}
