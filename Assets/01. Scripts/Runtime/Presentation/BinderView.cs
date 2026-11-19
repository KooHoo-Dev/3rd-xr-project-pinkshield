using System;
using System.Collections;
using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Interaction;
using CardShop.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.Presentation
{
    /// <summary>
    /// 바인더(보관함 겸 도감). 새 저장 시스템이 아니라 GameSession.Collection을 그리는 뷰입니다.
    /// 이 클래스는 칸 그리기 · 선택 · 집 도착 반짝임 · 이벤트 연결만 맡고, 일은 부품이 합니다 (AI-005 · 큰_클래스_나누기_제안 3장):
    ///   <see cref="BinderCursor"/> 탭 · 쪽 · 카드 찾아가기(순수, 테스트) · <see cref="StashedCards"/> 반짝일 카드(순수) ·
    ///   <see cref="BinderTabBar"/> 탭 버튼 · <see cref="BinderSocketStore"/> 표지 소켓에 꽂은 카드 날려 넣기.
    /// - 속성 탭: 전체 · 불 · 물 · 땅 · 공기 · 무 · 기타 (인기 속성 탭에 ★: 가게에서는 오늘, 집에서는 내일). 칸 번호는 탭과 상관없이 도감 번호
    /// - 칸 오브젝트는 한 쪽 분량만 두고 쪽을 넘길 때 다시 그립니다 (Quest 성능: 50장을 만들지 않음)
    /// - 판매 등록 등은 CardSelected 이벤트와 SelectedCard로 연결합니다 (DEC-023: 레이 클릭 방식).
    /// </summary>
    public sealed class BinderView : MonoBehaviour
    {
        [SerializeField] BinderSlotView[] slots;
        [SerializeField] Button prevButton;
        [SerializeField] Button nextButton;
        [SerializeField] TMP_Text pageLabel;
        [Tooltip("속성 탭 버튼. 순서는 BinderTabs.Order (전체 · 불 · 물 · 땅 · 공기 · 무 · 기타). 비워 두면 탭 없음")]
        [SerializeField] Button[] tabButtons;
        [Tooltip("고른 탭의 글자 색")]
        [SerializeField] Color tabSelectedText = new Color(1f, 0.85f, 0.3f);
        [SerializeField] CardRarityStyles styles;
        public CardRarityStyles Styles => styles;
        [Tooltip("표지 소켓 (카드를 꽂는 곳). 비워 두면 보기 전용")]
        [SerializeField] BinderCoverSocket coverSocket;
        [Tooltip("꽂은 카드가 칸까지 날아가는 시간 (초)")]
        [SerializeField] float flySeconds = 0.35f;
        [Tooltip("집에 도착한 뒤 새로 넣은 카드 칸이 반짝이기까지 기다리는 시간 (초). 장면 전환(페이드)이 끝난 뒤에 보이도록")]
        [SerializeField] float arrivalFlashDelay = 2.6f;

        GameContext context;
        Collection collection;
        BinderCursor cursor;
        BinderTabBar tabBar;
        BinderSocketStore socketStore;
        CardRevealer revealer;
        readonly StashedCards stashed = new StashedCards();
        bool dirty = true;
        bool atHome;
        bool flashScheduled;

        /// <summary>발견한 카드 칸을 골랐을 때 (판매 등록 등 다음 단계의 연결 지점).</summary>
        public event Action<CardData> CardSelected;

        /// <summary>지금 골라 둔 카드. 없으면 null. 보유 장수는 GameSession.Collection.CountOf로 확인하세요(판매로 0장일 수 있음).</summary>
        public CardData SelectedCard { get; private set; }

        public int Page => cursor != null ? cursor.Page : 0;
        public int PageCount => cursor != null ? cursor.PageCount : 1;
        public BinderTab Tab => cursor != null ? cursor.Tab : BinderTab.All;

        // 카드 DB가 준비된 뒤 처음 쓸 때 만듦
        BinderCursor Cursor
        {
            get
            {
                if (cursor == null && context != null && context.Cards != null && slots != null && slots.Length > 0)
                    cursor = new BinderCursor(context.Cards.cards, slots.Length);
                return cursor;
            }
        }

        void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i; // 람다가 반복 변수 대신 이 칸 번호를 기억하도록
                slots[i].Button.onClick.AddListener(() => OnSlotClicked(index));
            }
            tabBar = new BinderTabBar(tabButtons, tabSelectedText, ShowTab);
            socketStore = new BinderSocketStore(this, coverSocket, SlotFor, () => flySeconds);
        }

        void OnEnable()
        {
            prevButton.onClick.AddListener(PreviousPage);
            nextButton.onClick.AddListener(NextPage);
            socketStore.Enable();

            context = GameContext.Instance;
            if (context != null)
            {
                context.Events.SessionStarted += OnSessionStarted;
                context.Events.PhaseChanged += OnPhaseChanged;
                Bind(context.Session);
            }
            dirty = true;

            // 결과함의 "보관함에 넣기"를 바인더에 연결 (가게 더미 → 집 바인더). 테이블에 쌓아 둔 카드 전체(오래된 것 포함)를 퇴근 때 알려 줌
            revealer = FindAnyObjectByType<CardRevealer>();
            if (revealer != null) revealer.PileStashed += OnPileStashed;
        }

        void OnDisable()
        {
            prevButton.onClick.RemoveListener(PreviousPage);
            nextButton.onClick.RemoveListener(NextPage);
            socketStore.Disable();
            if (context != null)
            {
                context.Events.SessionStarted -= OnSessionStarted;
                context.Events.PhaseChanged -= OnPhaseChanged;
            }
            if (revealer != null) revealer.PileStashed -= OnPileStashed;
            revealer = null;
            Bind(null);
            context = null;
        }

        // 새 게임이면 새 Collection을 다시 구독하고 첫 페이지로
        void OnSessionStarted(GameSession session)
        {
            Bind(session);
            Cursor?.Reset();
            ClearSelection();
            stashed.Clear();
            flashScheduled = false;
        }

        // ------------------------------------------------------------------ 집 도착: 보관함에 들어온 카드 반짝임

        /// <summary>
        /// 퇴근 때 보관함으로 보낸 카드들 (CardRevealer.PileStashed). 가게에서는 더미가 사라지는 연출만 하고,
        /// 집에 도착하면 이 카드들의 칸을 보여 주며 반짝입니다.
        /// </summary>
        void OnPileStashed(IReadOnlyList<CardData> cards)
        {
            var ids = new List<string>(cards.Count);
            foreach (CardData c in cards) if (c != null) ids.Add(c.id);
            MarkStored(ids);
        }

        public void MarkStored(IEnumerable<string> cardIds)
        {
            stashed.Add(cardIds);
            // 집 도착 때 자동 보관되는 경우(CardRevealer가 먼저 반응)에도 놓치지 않도록
            if (atHome) ScheduleArrival();
        }

        void OnPhaseChanged(GamePhaseId from, GamePhaseId to)
        {
            atHome = to == GamePhaseId.Home;
            dirty = true; // 날이 바뀌면 오늘 인기 속성(★)도 바뀜
            if (atHome && stashed.Count > 0) ScheduleArrival();
        }

        void ScheduleArrival()
        {
            if (flashScheduled || stashed.Count == 0) return;
            flashScheduled = true;
            // 가장 최근에 넣은 카드가 있는 페이지를 펼쳐 둠 (장면 전환 중이라 플레이어는 못 봄)
            ShowCard(stashed.Last);
            StartCoroutine(FlashStoredLater());
        }

        IEnumerator FlashStoredLater()
        {
            yield return new WaitForSeconds(arrivalFlashDelay);
            if (Cursor != null)
                for (int i = 0; i < slots.Length; i++)
                {
                    string id = Cursor.IdAt(i);
                    if (id != null && stashed.Contains(id)) slots[i].Flash();
                }
            stashed.Clear();
            flashScheduled = false;
        }

        void Bind(GameSession session)
        {
            if (collection != null) collection.Changed -= OnCollectionChanged;
            collection = session?.Collection;
            if (collection != null) collection.Changed += OnCollectionChanged;
            dirty = true;
        }

        // 상자 하나에 50장이 들어와도 한 프레임에 한 번만 다시 그림
        void OnCollectionChanged(string _) => dirty = true;

        void LateUpdate()
        {
            if (dirty) Refresh();
        }

        // ------------------------------------------------------------------ 쪽 · 탭

        public void NextPage() => ShowPage(Page + 1);
        public void PreviousPage() => ShowPage(Page - 1);

        public void ShowPage(int index)
        {
            Cursor?.ShowPage(index);
            Refresh();
        }

        /// <summary>속성 탭을 바꾸고 그 탭의 첫 페이지를 보여 줍니다.</summary>
        public void ShowTab(BinderTab next)
        {
            Cursor?.ShowTab(next);
            Refresh();
        }

        /// <summary>그 카드가 있는 페이지로 넘깁니다. 지금 탭에 없는 카드면 그 카드의 속성 탭으로 바꿉니다. 바인더에 없는 카드면 false.</summary>
        public bool ShowCard(string cardId)
        {
            if (Cursor == null || cardId == null || !Cursor.ShowCard(context.Cards.Get(cardId))) return false;
            Refresh();
            return true;
        }

        // 표지 소켓에 꽂은 카드: 그 카드 쪽을 펼치고 칸을 돌려줌
        BinderSlotView SlotFor(CardData card)
        {
            if (card == null || !ShowCard(card.id)) return null;
            int slot = Cursor.SlotOf(card.id);
            return slot >= 0 && slot < slots.Length ? slots[slot] : null;
        }

        void Refresh()
        {
            dirty = false;
            BinderCursor c = Cursor;
            for (int i = 0; i < slots.Length; i++)
            {
                string id = c?.IdAt(i);
                CardData card = id != null ? context.Cards.Get(id) : null;
                bool discovered = card != null && collection != null && collection.IsDiscovered(id);
                int count = card != null && collection != null ? collection.CountOf(id) : 0;
                int listed = card != null && context.Session != null ? context.Session.SalesDisplay.FindAll(x => x == id).Count : 0;
                int number = c != null && id != null ? c.NumberOf(id) : 0; // 탭과 상관없이 도감 번호
                slots[i].Show(card, number, discovered, count, listed, styles);
                slots[i].SetSelected(card != null && card == SelectedCard);
            }

            prevButton.interactable = c != null && c.HasPrevious;
            nextButton.interactable = c != null && c.HasNext;
            CardElement star = StarElement();
            tabBar.Redraw(Tab, star);
            if (pageLabel != null)
            {
                int total = c != null ? c.FullLayout.CardCount : 0;
                int found = collection != null ? collection.DiscoveredCount : 0;
                string tabName = Tab == BinderTab.All ? "" : $"{BinderTabs.Label(Tab)} · ";
                string starHint = star == CardElement.None ? "" : atHome ? "  ·  ★ 내일 인기" : "  ·  ★ 오늘 인기";
                int money = context != null && context.Session != null ? context.Session.Wallet.Money : 0; // 판매하면 Collection.Changed로 다시 그려짐 (FIX B3)
                if (!pageLabel.enableAutoSizing) // 한 줄이 넘치면 줄임, 원래 크기(30)보다 커지지는 않게 (씬 다시 빌드 없이)
                { pageLabel.fontSizeMax = pageLabel.fontSize; pageLabel.fontSizeMin = 20f; pageLabel.enableAutoSizing = true; }
                pageLabel.text = $"<color=#FFD84D>내 자산 {MoneyText.Format(money)}</color>  ·  {tabName}{Page + 1} / {PageCount} 쪽  ·  도감 {found} / {total}{starHint}";
            }
        }

        // 집에서 등록한 카드는 내일 영업 때 팔리므로 집에서는 내일 인기 속성에 ★ (마지막 날은 내일이 없어 ★ 없음, 11-19 A4)
        CardElement StarElement()
        {
            if (context == null || context.Session == null || context.PopularElements == null) return CardElement.None;
            return atHome ? context.PopularElements.For(context.Session.Day + 1) : context.PopularToday;
        }

        // ------------------------------------------------------------------ 선택 (판매 등록 연결 지점)

        void OnSlotClicked(int slotIndex)
        {
            string id = Cursor?.IdAt(slotIndex);
            if (id == null || collection == null || !collection.IsDiscovered(id)) return;
            Select(context.Cards.Get(id));
        }

        /// <summary>카드를 고릅니다. 발견하지 않은 카드는 고를 수 없습니다.</summary>
        public void Select(CardData card)
        {
            if (card == null || collection == null || !collection.IsDiscovered(card.id)) return;
            SelectedCard = card;
            ShowCard(card.id);
            CardSelected?.Invoke(card);
        }

        public void ClearSelection()
        {
            SelectedCard = null;
            dirty = true;
        }
    }
}
