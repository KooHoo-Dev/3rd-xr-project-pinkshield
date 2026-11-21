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
    /// 모니터 쪽 [슬라임마켓] (컴퓨터 3단계, 경제시스템_설계 14-3, DEC-040 U2).
    /// 바인더에서 고른 카드(수퍼레어 이상)를 1장 올려 두면 밤사이 팔릴 수 있습니다 (판정 · 값은 OnlineStallRules, 밤은 DayEndPhase).
    /// 컴퓨터 3단계 전에는 "잠김" 안내만 보입니다. 화면은 SlimeOS 창 하나 (HomeComputer.CreateWindowCanvas).
    /// </summary>
    public sealed class OnlineStallPage : HomePanel
    {
        const string PageName = "슬라임마켓";

        BinderView binder;
        TMP_Text status, detail;
        Button putButton, takeButton;

        public static OnlineStallPage Create(HomeComputer computer, BinderView binder, TMP_FontAsset font)
        {
            if (computer == null) return null;
            RectTransform rt = computer.CreateWindowCanvas("OnlineStallPage", 0.01f, out GameObject content);
            rt.gameObject.SetActive(false); // 설정을 다 넣은 뒤 켬 (OnEnable 순서)
            SlimeOsWindow.Apply(content.transform, PageName + "  <size=75%>카드 온라인 판매 · 밤사이 팔릴지도?</size>", font);

            var page = rt.gameObject.AddComponent<OnlineStallPage>();
            page.content = content;
            page.binder = binder;
            float top = 1f - (SlimeOsWindow.BarPx + 30f) / HomeComputer.WindowH;
            page.status = WorldUiKit.AddText(content.transform, "Status", "", new Vector2(0.06f, top - 0.26f), new Vector2(0.94f, top), 40f, UiTheme.SoftText, font);
            page.detail = WorldUiKit.AddText(content.transform, "Detail", "", new Vector2(0.06f, 0.26f), new Vector2(0.94f, top - 0.28f), 28f, UiTheme.SoftSub, font);
            page.detail.enableAutoSizing = true;
            page.detail.fontSizeMin = 18f;
            page.detail.fontSizeMax = 28f;

            page.putButton = WorldUiKit.AddButton(content.transform, "고른 카드 올리기", new Vector2(0.08f, 0.07f), new Vector2(0.48f, 0.2f), font);
            page.takeButton = WorldUiKit.AddButton(content.transform, "장터에서 내리기", new Vector2(0.52f, 0.07f), new Vector2(0.92f, 0.2f), font);
            UiSkin.StyleButton(page.putButton, UiButtonKind.Primary);
            UiSkin.StyleButton(page.takeButton, UiButtonKind.Paper);
            page.putButton.onClick.AddListener(page.OnPut);
            page.takeButton.onClick.AddListener(page.OnTake);

            computer.AddSwipe(rt);
            computer.AddPage(new CanvasPage(rt), PageName);
            rt.gameObject.SetActive(true);
            return page;
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

        void OnCardSelected(CardData _) => MarkDirty();

        void OnPut()
        {
            GameContext ctx = Context;
            CardData card = binder != null ? binder.SelectedCard : null;
            if (ctx == null || card == null || !Unlocked(ctx)) return;
            if (ctx.OnlineStall.Put(ctx.Session, card.id))
            {
                Sfx.Play(SoundId.UiClick, transform.position);
                ctx.Events.RaiseNotice($"「{card.displayName}」을(를) 슬라임마켓에 올렸어요. 밤사이 팔리면 아침에 알려 드려요");
            }
            else ctx.Events.RaiseNotice("슬라임마켓에는 수퍼레어 이상, 가지고 있는 카드만 올릴 수 있어요");
            MarkDirty();
        }

        void OnTake()
        {
            GameContext ctx = Context;
            if (ctx != null && ctx.OnlineStall.Take(ctx.Session)) Sfx.Play(SoundId.UiClick, transform.position);
            MarkDirty();
        }

        static bool Unlocked(GameContext ctx) => ctx.Upgrades.HasOnlineStall(ctx.Session.Upgrades);

        protected override void Redraw()
        {
            GameContext ctx = Context;
            if (ctx == null || ctx.Session == null || status == null) return;
            GameSession s = ctx.Session;
            bool unlocked = Unlocked(ctx);
            CardData selected = binder != null ? binder.SelectedCard : null;
            CardData onStall = s.OnlineStall != null ? ctx.Cards.Get(s.OnlineStall) : null;

            putButton.interactable = unlocked && selected != null && ctx.OnlineStall.CanPut(selected) && s.Collection.CountOf(selected.id) > 0;
            takeButton.interactable = unlocked && onStall != null;

            if (!unlocked)
            {
                status.text = "잠김";
                detail.text = "슬라임 홈쇼핑에서 컴퓨터 3단계(카드 시세표)를 사면 열려요.\n카드 1장을 올려 두면 자는 동안 누군가 사 갈지도 몰라요.";
                return;
            }

            MarketDay tomorrow = ctx.Market.For(s.Day + 1);
            if (onStall != null)
            {
                status.text = $"올린 카드  {onStall.displayName}";
                detail.text = $"내일 시세 장터 값  {MoneyText.Format(ctx.OnlineStall.Price(onStall, tomorrow))}  (수수료 {Mathf.RoundToInt(ctx.Economy.onlineStallCommission * 100f)}%)\n" +
                              $"밤사이 팔릴 확률 약 {Mathf.RoundToInt(ctx.OnlineStall.SellChance(onStall) * 100f)}%\n" +
                              "다른 카드를 올리면 지금 카드는 바인더로 돌아와요.";
            }
            else
            {
                status.text = "비어 있음";
                detail.text = selected != null && ctx.OnlineStall.CanPut(selected)
                    ? $"고른 카드  {selected.displayName}  →  장터 값 {MoneyText.Format(ctx.OnlineStall.Price(selected, tomorrow))} · 팔릴 확률 약 {Mathf.RoundToInt(ctx.OnlineStall.SellChance(selected) * 100f)}%"
                    : "바인더에서 수퍼레어 이상 카드를 고른 뒤 [고른 카드 올리기]";
            }
        }
    }
}
