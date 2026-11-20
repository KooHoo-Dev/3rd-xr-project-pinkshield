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
    /// 집 책상 바인더 옆의 판매 패널 (시연 피드백 7-4).
    /// 바인더에서 카드를 고르면 이름 · 보유 장수 · 판매 등록 가능 여부 · 즉시 판매가 · 판매 등록 칸을 보여 주고,
    /// [판매 등록] [즉시 판매] [레어 이하 모두 팔기] 버튼과 판매 등록 중인 카드 목록(누르면 판매 등록 취소)을 둡니다.
    /// 모두 팔기는 실수 방지로 한 번 누르면 견적(장수 · 금액)을 보여 주고, ConfirmSeconds 안에 한 번 더 누르면 팝니다.
    /// 규칙은 순수 C# SalesRules가 하고 이 컴포넌트는 버튼과 글자만 잇습니다. 집(Home)에서만 보입니다.
    /// </summary>
    public sealed class SalesPanel : HomePanel
    {
        // 문구는 한 곳에 모읍니다.
        const string NoSelection = "바인더에서 카드를 골라 주세요";
        const string DisplayHeader = "판매 등록 중";
        const string DisplayEmpty = "판매 등록 중인 카드 없음";
        const float ConfirmSeconds = 4f;
        const float MinConfirmGap = 0.3f; // 한 번 누른 것이 두 번 들어와도(레이 클릭 중복) 바로 팔리지 않게
        // 모두 팔기 대상: 이 등급 이하 (판매 등록은 수퍼레어 이상만 되므로 그 아래를 정리하는 버튼)
        const CardRarity SellAllMax = CardRarity.Rare;

        [SerializeField] BinderView binder;
        [SerializeField] TMP_Text selectedLabel;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] TMP_Text messageLabel;
        [SerializeField] Button displayButton;
        [SerializeField] Button sellButton;
        [Tooltip("레어 이하 모두 팔기 (비우면 기능 없음 — Shop 씬에서 버튼 연결)")]
        [SerializeField] Button sellAllButton;
        [Tooltip("판매 등록 중인 카드 버튼(판매 등록 칸 수만큼). 누르면 판매 등록 취소")]
        [SerializeField] Button[] displayedButtons;
        [Tooltip("판매 등록 모두 내리기 (비우면 런타임에 [모두 팔기] 버튼을 복제해 목록 제목 오른쪽에 만듦, DEC-037)")]
        [SerializeField] Button undisplayAllButton;

        string message = "";
        float sellAllArmedUntil = -1f; // 이 시각까지 한 번 더 누르면 팖

        protected override void OnEnable()
        {
            if (undisplayAllButton == null) undisplayAllButton = CreateUndisplayAllButton();
            if (undisplayAllButton != null) undisplayAllButton.onClick.AddListener(OnUndisplayAllClicked);
            displayButton.onClick.AddListener(OnDisplayClicked);
            sellButton.onClick.AddListener(OnSellClicked);
            if (sellAllButton != null) sellAllButton.onClick.AddListener(OnSellAllClicked);
            for (int i = 0; i < displayedButtons.Length; i++)
            {
                int index = i; // 람다가 반복 변수 대신 이 칸 번호를 기억하도록
                displayedButtons[i].onClick.AddListener(() => OnDisplayedClicked(index));
            }
            if (binder != null) binder.CardSelected += OnCardSelected;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            displayButton.onClick.RemoveListener(OnDisplayClicked);
            sellButton.onClick.RemoveListener(OnSellClicked);
            if (sellAllButton != null) sellAllButton.onClick.RemoveListener(OnSellAllClicked);
            if (undisplayAllButton != null) undisplayAllButton.onClick.RemoveListener(OnUndisplayAllClicked);
            foreach (Button b in displayedButtons) b.onClick.RemoveAllListeners();
            if (binder != null) binder.CardSelected -= OnCardSelected;
            base.OnDisable();
        }

        // 새 게임 · 집에 들어올 때 · 다른 카드를 고를 때는 지난 안내 문구를 지움
        protected override void Bind(GameSession session)
        {
            message = "";
            base.Bind(session);
        }

        protected override void OnPhaseChanged(GamePhaseId phase)
        {
            if (phase == GamePhaseId.Home) message = "";
            base.OnPhaseChanged(phase);
        }

        void OnCardSelected(CardData _)
        {
            message = "";
            MarkDirty();
        }

        // ------------------------------------------------------------------ 버튼

        void OnDisplayClicked()
        {
            CardData card = binder != null ? binder.SelectedCard : null;
            if (card == null || Context == null) return;

            switch (Context.Sales.Display(Context.Session, card.id))
            {
                case DisplayResult.Ok:
                    message = $"{card.displayName} 상점 판매 등록 완료";
                    Context.Events.RaiseHappened(GameMoment.SalesRegistered);
                    break;
                case DisplayResult.RarityTooLow: message = $"판매 등록은 {RarityName(Context.Economy.minDisplayRarity)} 이상만 가능해요"; break;
                case DisplayResult.DisplayFull: message = "판매 등록 칸이 가득 찼어요"; break;
                case DisplayResult.NotOwned: message = "보유한 카드가 없어요"; break;
                default: message = "판매 등록할 수 없는 카드예요"; break;
            }
            Refresh();
        }

        void OnSellClicked()
        {
            CardData card = binder != null ? binder.SelectedCard : null;
            if (card == null || Context == null) return;

            bool sold = Context.Sales.TrySellInstantly(Context.Session, card.id, out int price);
            bool bulkOnly = !sold && Context.Session.Collection.CountOf(card.id) > 0 && Context.Sales.InstantPrice(card, Context.Session) <= 0;
            message = sold ? $"{card.displayName} 판매  {MoneyText.Signed(price)}"
                : bulkOnly ? $"{RarityName(card.rarity)}은 낱장 매입가가 없어요\n'{RarityName(SellAllMax)} 이하 모두 팔기'로 묶어서 팔아요"
                : "보유한 카드가 없어요";
            if (sold) { Sfx.Play(SoundId.MoneyIn, transform.position); Context.Events.RaiseHappened(GameMoment.InstantSold); }
            Refresh();
        }

        // 확인 시간이 지나면 "한 번 더 누르면" 안내를 지움 (그대로 두면 다시 눌러도 견적만 다시 나옴)
        void Update()
        {
            if (sellAllArmedUntil < 0f || Time.unscaledTime <= sellAllArmedUntil) return;
            sellAllArmedUntil = -1f;
            message = "";
            Refresh();
        }

        void OnSellAllClicked()
        {
            if (Context == null) return;
            GameSession s = Context.Session;
            string what = $"{RarityName(SellAllMax)} 이하";

            float now = Time.unscaledTime;
            if (now <= sellAllArmedUntil && now < sellAllArmedUntil - ConfirmSeconds + MinConfirmGap) return;
            if (now > sellAllArmedUntil)
            {
                int count = Context.Sales.QuoteSellAllUpTo(s, SellAllMax, out int quote);
                if (count == 0) message = $"{what} 카드가 없어요";
                else
                {
                    message = $"{what} {count}장  {MoneyText.Signed(quote)}\n한 번 더 누르면 모두 팔아요";
                    sellAllArmedUntil = Time.unscaledTime + ConfirmSeconds;
                }
            }
            else
            {
                sellAllArmedUntil = -1f;
                int sold = Context.Sales.SellAllUpTo(s, SellAllMax, out int total);
                message = sold > 0 ? $"{what} {sold}장 판매  {MoneyText.Signed(total)}" : $"{what} 카드가 없어요";
                if (sold > 0) Sfx.Play(SoundId.MoneyIn, transform.position);
            }
            Refresh();
        }

        void OnDisplayedClicked(int index)
        {
            if (Context == null || index >= Context.Session.SalesDisplay.Count) return;
            string id = Context.Session.SalesDisplay[index];
            if (Context.Sales.TryUndisplay(Context.Session, id))
            {
                CardData card = Context.Cards.Get(id);
                message = $"{(card != null ? card.displayName : id)} 판매 등록 취소";
            }
            Refresh();
        }

        // 판매 등록은 다음 날까지 남으므로(DEC-037) 한 번에 모두 보유로 돌려놓는 버튼. 되돌릴 수 있어 확인은 없음
        void OnUndisplayAllClicked()
        {
            if (Context == null) return;
            int n = Context.Sales.ReturnUnsold(Context.Session);
            message = n > 0 ? $"판매 등록 {n}장 모두 내림 (바인더로)" : DisplayEmpty;
            Refresh();
        }

        // 씬 판을 다시 빌드하지 않도록 [모두 팔기]를 복제해 "판매 등록 중" 제목 줄 오른쪽에 둠
        Button CreateUndisplayAllButton()
        {
            if (sellAllButton == null || displayedButtons == null || displayedButtons.Length == 0) return null;
            Button b = Instantiate(sellAllButton, sellAllButton.transform.parent);
            b.name = "Button_모두 내리기";
            b.onClick.RemoveAllListeners();
            var first = (RectTransform)displayedButtons[0].transform;
            var rt = (RectTransform)b.transform;
            float bottom = first.anchorMax.y + 0.01f;
            rt.anchorMin = new Vector2(0.64f, bottom);
            rt.anchorMax = new Vector2(first.anchorMax.x, bottom + 0.04f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            if (label != null) { label.text = "모두 내리기"; label.fontSize = 18f; }
            // 제목 글자는 왼쪽으로 좁혀 버튼과 겹치지 않게
            Transform header = sellAllButton.transform.parent.Find("ListHeader");
            if (header is RectTransform h)
            {
                h.anchorMax = new Vector2(0.62f, h.anchorMax.y);
                if (h.TryGetComponent(out TMP_Text t)) t.alignment = TextAlignmentOptions.Left;
            }
            return b;
        }

        // ------------------------------------------------------------------ 화면

        // 등급 이름은 CardRarityStyles 에셋의 label이 원본 (바인더와 같은 에셋)
        string RarityName(CardRarity r)
        {
            CardRarityStyles styles = binder != null ? binder.Styles : null;
            return styles != null ? styles.Get(r).label : r.ToString();
        }

        // 매입가 한 줄 (DEC-041): 노멀은 묶음 값, 판매 등록할 수 있는 카드는 가게 값과 비교
        string BuybackLine(CardData card, GameSession session)
        {
            float value = Context.Sales.InstantValue(card, session);
            int price = Context.Sales.InstantPrice(card, session);
            if (price <= 0 && value > 0f)
                return $"매입가  {Mathf.RoundToInt(1f / value)}장에 {MoneyText.Format(1)} (모두 팔기)";
            string line = $"매입가  {MoneyText.Format(price)}";
            if (Context.Sales.CanDisplay(card))
                line += $"  ·  가게에 올리면 {MoneyText.Format(Context.EconomyRules.SellPrice(card, MarketDay.None))}+";
            return line;
        }

        protected override void Redraw()
        {
            if (Context == null || Context.Session == null) return;
            GameSession session = Context.Session;
            CardData card = binder != null ? binder.SelectedCard : null;

            if (card == null)
            {
                selectedLabel.text = NoSelection;
            }
            else
            {
                int count = session.Collection.CountOf(card.id);
                string display = Context.Sales.CanDisplay(card)
                    ? "판매 등록 가능"
                    : $"판매 등록 불가 ({RarityName(Context.Economy.minDisplayRarity)} 이상만)";
                selectedLabel.text =
                    $"{card.displayName}  ({RarityName(card.rarity)})\n" +
                    $"보유 {count}장\n{display}\n{BuybackLine(card, session)}";
            }
            displayButton.interactable = card != null;
            sellButton.interactable = card != null;
            if (sellAllButton != null)
            {
                bool armed = Time.unscaledTime <= sellAllArmedUntil;
                sellAllButton.GetComponentInChildren<TMP_Text>().text = armed ? "한 번 더 눌러 판매" : $"{RarityName(SellAllMax)} 이하 모두 팔기";
            }

            if (undisplayAllButton != null) undisplayAllButton.interactable = session.SalesDisplay.Count > 0;

            int slots = Context.Sales.SlotCount(session); // 바인더 업그레이드로 늘 수 있음
            displayedButtons = SalesListGrower.Grow(displayedButtons, slots, OnDisplayedClicked);
            statusLabel.text = $"판매 등록 칸 {session.SalesDisplay.Count} / {slots}   ·   내 자산 {MoneyText.Format(session.Wallet.Money)}";
            messageLabel.text = message;

            for (int i = 0; i < displayedButtons.Length; i++)
            {
                bool has = i < session.SalesDisplay.Count;
                displayedButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                CardData shown = Context.Cards.Get(session.SalesDisplay[i]);
                displayedButtons[i].GetComponentInChildren<TMP_Text>().text =
                    (shown != null ? shown.displayName : session.SalesDisplay[i]) + "  (취소)";
            }
        }
    }
}
