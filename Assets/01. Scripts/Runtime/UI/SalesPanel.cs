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
    /// [판매 등록] [즉시 판매] 버튼과 판매 등록 중인 카드 목록(누르면 판매 등록 취소)을 둡니다.
    /// 규칙은 순수 C# SalesRules가 하고 이 컴포넌트는 버튼과 글자만 잇습니다. 집(Home)에서만 보입니다.
    /// </summary>
    public sealed class SalesPanel : MonoBehaviour
    {
        // 문구는 한 곳에 모읍니다.
        const string NoSelection = "바인더에서 카드를 골라 주세요";
        const string DisplayHeader = "판매 등록 중";
        const string DisplayEmpty = "판매 등록 중인 카드 없음";

        static readonly string[] RarityNames = { "노말", "레어", "수퍼레어", "유니크", "레전더리" };

        [Tooltip("패널 내용을 담은 오브젝트. 집이 아닐 때 꺼집니다.")]
        [SerializeField] GameObject content;
        [SerializeField] BinderView binder;
        [SerializeField] TMP_Text selectedLabel;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] TMP_Text messageLabel;
        [SerializeField] Button displayButton;
        [SerializeField] Button sellButton;
        [Tooltip("판매 등록 중인 카드 버튼(판매 등록 칸 수만큼). 누르면 판매 등록 취소")]
        [SerializeField] Button[] displayedButtons;

        GameContext context;
        Collection collection;
        string message = "";
        bool dirty;

        void OnEnable()
        {
            displayButton.onClick.AddListener(OnDisplayClicked);
            sellButton.onClick.AddListener(OnSellClicked);
            for (int i = 0; i < displayedButtons.Length; i++)
            {
                int index = i; // 람다가 반복 변수 대신 이 칸 번호를 기억하도록
                displayedButtons[i].onClick.AddListener(() => OnDisplayedClicked(index));
            }
            if (binder != null) binder.CardSelected += OnCardSelected;

            context = GameContext.Instance;
            if (context != null)
            {
                context.Events.PhaseChanged += OnPhaseChanged;
                context.Events.SessionStarted += OnSessionStarted;
                Bind(context.Session);
            }
            SetVisible(false);
        }

        void OnDisable()
        {
            displayButton.onClick.RemoveListener(OnDisplayClicked);
            sellButton.onClick.RemoveListener(OnSellClicked);
            foreach (Button b in displayedButtons) b.onClick.RemoveAllListeners();
            if (binder != null) binder.CardSelected -= OnCardSelected;
            if (context != null)
            {
                context.Events.PhaseChanged -= OnPhaseChanged;
                context.Events.SessionStarted -= OnSessionStarted;
            }
            Bind(null);
            context = null;
        }

        void OnSessionStarted(GameSession session)
        {
            Bind(session);
            message = "";
            dirty = true;
        }

        void Bind(GameSession session)
        {
            if (collection != null) collection.Changed -= OnCollectionChanged;
            collection = session?.Collection;
            if (collection != null) collection.Changed += OnCollectionChanged;
            dirty = true;
        }

        // 한 프레임에 한 번만 다시 그림
        void OnCollectionChanged(string _) => dirty = true;

        void OnCardSelected(CardData _)
        {
            message = "";
            dirty = true;
        }

        void OnPhaseChanged(GamePhaseId from, GamePhaseId to)
        {
            bool home = to == GamePhaseId.Home;
            if (home)
            {
                message = "";
                Refresh();
            }
            SetVisible(home);
        }

        void SetVisible(bool visible)
        {
            if (content != null) content.SetActive(visible);
        }

        void LateUpdate()
        {
            if (dirty && content != null && content.activeSelf) Refresh();
        }

        // ------------------------------------------------------------------ 버튼

        void OnDisplayClicked()
        {
            CardData card = binder != null ? binder.SelectedCard : null;
            if (card == null || context == null) return;

            switch (context.Sales.Display(context.Session, card.id))
            {
                case DisplayResult.Ok: message = $"{card.displayName} 판매 등록"; break;
                case DisplayResult.RarityTooLow: message = $"판매 등록은 {RarityNames[(int)context.Economy.minDisplayRarity]} 이상만 가능해요"; break;
                case DisplayResult.DisplayFull: message = "판매 등록 칸이 가득 찼어요"; break;
                case DisplayResult.NotOwned: message = "보유한 카드가 없어요"; break;
                default: message = "판매 등록할 수 없는 카드예요"; break;
            }
            Refresh();
        }

        void OnSellClicked()
        {
            CardData card = binder != null ? binder.SelectedCard : null;
            if (card == null || context == null) return;

            message = context.Sales.TrySellInstantly(context.Session, card.id, out int price)
                ? $"{card.displayName} 판매  +{price}"
                : "보유한 카드가 없어요";
            Refresh();
        }

        void OnDisplayedClicked(int index)
        {
            if (context == null || index >= context.Session.SalesDisplay.Count) return;
            string id = context.Session.SalesDisplay[index];
            if (context.Sales.TryUndisplay(context.Session, id))
            {
                CardData card = context.Cards.Get(id);
                message = $"{(card != null ? card.displayName : id)} 판매 등록 취소";
            }
            Refresh();
        }

        // ------------------------------------------------------------------ 화면

        void Refresh()
        {
            dirty = false;
            if (context == null || context.Session == null) return;
            GameSession session = context.Session;
            CardData card = binder != null ? binder.SelectedCard : null;

            if (card == null)
            {
                selectedLabel.text = NoSelection;
            }
            else
            {
                int count = session.Collection.CountOf(card.id);
                string display = context.Sales.CanDisplay(card)
                    ? "판매 등록 가능"
                    : $"판매 등록 불가 ({RarityNames[(int)context.Economy.minDisplayRarity]} 이상만)";
                selectedLabel.text =
                    $"{card.displayName}  ({RarityNames[(int)card.rarity]})\n" +
                    $"보유 {count}장\n{display}\n즉시 판매가  {context.Sales.InstantPrice(card)}";
            }
            displayButton.interactable = card != null;
            sellButton.interactable = card != null;

            statusLabel.text = $"판매 등록 칸 {session.SalesDisplay.Count} / {context.Economy.salesDisplaySlots}   ·   보유 재화 {session.Wallet.Money}";
            messageLabel.text = message;

            for (int i = 0; i < displayedButtons.Length; i++)
            {
                bool has = i < session.SalesDisplay.Count;
                displayedButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                CardData shown = context.Cards.Get(session.SalesDisplay[i]);
                displayedButtons[i].GetComponentInChildren<TMP_Text>().text =
                    (shown != null ? shown.displayName : session.SalesDisplay[i]) + "  (취소)";
            }
        }
    }
}
