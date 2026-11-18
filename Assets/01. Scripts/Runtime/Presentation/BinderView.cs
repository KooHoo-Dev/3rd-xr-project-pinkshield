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
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Presentation
{
    /// <summary>
    /// 바인더(보관함 겸 도감). 새 저장 시스템이 아니라 GameSession.Collection을 그리는 뷰입니다.
    /// - 칸 배치: BinderLayout (CardDatabase 순서, 한 페이지 = slots 수)
    /// - 속성 탭(BinderTabs): 전체 · 불 · 물 · 땅 · 공기 · 무 · 기타. 탭을 고르면 그 속성 카드만 모아 보여 줌
    ///   (커뮤니티에서 본 인기 속성을 바로 찾기). 오늘 인기 속성 탭에는 ★. 칸 번호는 탭과 상관없이 도감 번호
    /// - 칸 오브젝트는 한 페이지 분량만 두고 페이지를 넘길 때 다시 그립니다 (Quest 성능: 50장을 만들지 않음)
    /// - 표지 소켓에 카드를 꽂으면 그 카드가 자기 칸으로 날아가 사라집니다. 카드는 개봉할 때 이미
    ///   Collection에 들어가 있으므로 데이터는 바꾸지 않습니다(정리 연출).
    /// - 판매 등록 등 다음 기능은 CardSelected 이벤트와 SelectedCard로 연결합니다.
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
        BinderLayout layout;     // 지금 탭에 보이는 카드만
        BinderLayout fullLayout; // 전체 카드 (도감 번호용)
        BinderTab tab = BinderTab.All;
        int page;
        bool dirty = true;
        int storing; // 날아가는 중인 카드 수 (그동안 표지 소켓을 잠금)
        CardRevealer revealer;
        BinderStash installedStash;
        readonly HashSet<string> pendingStored = new HashSet<string>();
        readonly List<string> pendingOrder = new List<string>();
        bool atHome;
        bool flashScheduled;

        /// <summary>발견한 카드 칸을 골랐을 때 (판매 등록 등 다음 단계의 연결 지점).</summary>
        public event Action<CardData> CardSelected;

        /// <summary>지금 골라 둔 카드. 없으면 null. 보유 장수는 GameSession.Collection.CountOf로 확인하세요(판매로 0장일 수 있음).</summary>
        public CardData SelectedCard { get; private set; }

        public int Page => page;
        public int PageCount => Layout != null ? Layout.PageCount : 1;
        public BinderTab Tab => tab;

        BinderLayout Layout
        {
            get
            {
                if (layout == null && context != null && context.Cards != null && slots != null && slots.Length > 0)
                    layout = new BinderLayout(BinderTabs.FilterIds(context.Cards.cards, tab), slots.Length);
                return layout;
            }
        }

        BinderLayout FullLayout
        {
            get
            {
                if (fullLayout == null && context != null && context.Cards != null && slots != null && slots.Length > 0)
                    fullLayout = new BinderLayout(BinderTabs.FilterIds(context.Cards.cards, BinderTab.All), slots.Length);
                return fullLayout;
            }
        }

        void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i; // 람다가 반복 변수 대신 이 칸 번호를 기억하도록
                slots[i].Button.onClick.AddListener(() => OnSlotClicked(index));
            }
            if (tabButtons != null)
                for (int i = 0; i < tabButtons.Length && i < BinderTabs.Order.Length; i++)
                {
                    if (tabButtons[i] == null) continue;
                    BinderTab t = BinderTabs.Order[i];
                    tabButtons[i].onClick.AddListener(() => ShowTab(t));
                }
        }

        void OnEnable()
        {
            prevButton.onClick.AddListener(PreviousPage);
            nextButton.onClick.AddListener(NextPage);
            if (coverSocket != null) coverSocket.selectEntered.AddListener(OnCardSocketed);

            context = GameContext.Instance;
            if (context != null)
            {
                context.Events.SessionStarted += OnSessionStarted;
                context.Events.PhaseChanged += OnPhaseChanged;
                Bind(context.Session);
            }
            dirty = true;

            // 결과함의 "보관함에 넣기"를 바인더에 연결 (가게 더미 → 집 바인더)
            revealer = FindAnyObjectByType<CardRevealer>();
            if (revealer != null)
            {
                installedStash = new BinderStash(this, revealer.Stash);
                revealer.Stash = installedStash;
                revealer.PileStashed += OnPileStashed; // 테이블에 쌓아 둔 카드 전체(오래된 것 포함)를 퇴근 때 알려 줌
            }
        }

        void OnDisable()
        {
            prevButton.onClick.RemoveListener(PreviousPage);
            nextButton.onClick.RemoveListener(NextPage);
            if (coverSocket != null) coverSocket.selectEntered.RemoveListener(OnCardSocketed);
            if (context != null)
            {
                context.Events.SessionStarted -= OnSessionStarted;
                context.Events.PhaseChanged -= OnPhaseChanged;
            }
            if (revealer != null) revealer.PileStashed -= OnPileStashed;
            if (revealer != null && installedStash != null && ReferenceEquals(revealer.Stash, installedStash))
                revealer.Stash = installedStash.Inner;
            revealer = null;
            installedStash = null;
            Bind(null);
            context = null;
            storing = 0;
            if (coverSocket != null) coverSocket.socketActive = true;
        }

        // 새 게임이면 새 Collection을 다시 구독하고 첫 페이지로
        void OnSessionStarted(GameSession session)
        {
            Bind(session);
            tab = BinderTab.All;
            layout = null;
            page = 0;
            ClearSelection();
            pendingStored.Clear();
            pendingOrder.Clear();
            flashScheduled = false;
        }

        /// <summary>
        /// 결과함에서 "보관함에 넣기"로 보낸 카드들 (BinderStash가 호출). 가게에서는 더미가 사라지는 연출만 하고,
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
            foreach (string id in cardIds)
                if (pendingStored.Add(id)) pendingOrder.Add(id);
            // 집 도착 때 자동 보관되는 경우(CardRevealer가 먼저 반응)에도 놓치지 않도록
            if (atHome) ScheduleArrival();
        }

        void OnPhaseChanged(GamePhaseId from, GamePhaseId to)
        {
            atHome = to == GamePhaseId.Home;
            dirty = true; // 날이 바뀌면 오늘 인기 속성(★)도 바뀜
            if (atHome && pendingOrder.Count > 0) ScheduleArrival();
        }

        void ScheduleArrival()
        {
            if (flashScheduled || pendingOrder.Count == 0) return;
            flashScheduled = true;
            // 가장 최근에 넣은 카드가 있는 페이지를 펼쳐 둠 (장면 전환 중이라 플레이어는 못 봄)
            ShowCard(pendingOrder[pendingOrder.Count - 1]);
            StartCoroutine(FlashStoredLater());
        }

        IEnumerator FlashStoredLater()
        {
            yield return new WaitForSeconds(arrivalFlashDelay);
            BinderLayout l = Layout;
            if (l != null)
                for (int i = 0; i < slots.Length; i++)
                {
                    string id = l.IdAt(page, i);
                    if (id != null && pendingStored.Contains(id)) slots[i].Flash();
                }
            pendingStored.Clear();
            pendingOrder.Clear();
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

        // ------------------------------------------------------------------ 페이지

        public void NextPage() => ShowPage(page + 1);
        public void PreviousPage() => ShowPage(page - 1);

        public void ShowPage(int index)
        {
            page = Layout != null ? Layout.ClampPage(index) : 0;
            Refresh();
        }

        /// <summary>속성 탭을 바꾸고 그 탭의 첫 페이지를 보여 줍니다.</summary>
        public void ShowTab(BinderTab next)
        {
            if (next != tab)
            {
                tab = next;
                layout = null; // 다음 접근 때 이 탭의 카드로 다시 만듦
            }
            ShowPage(0);
        }

        /// <summary>
        /// 그 카드가 있는 페이지로 넘깁니다. 지금 탭에 없는 카드면 그 카드의 속성 탭으로 바꿉니다.
        /// 바인더에 없는 카드면 false.
        /// </summary>
        public bool ShowCard(string cardId)
        {
            if (Layout == null) return false;
            if (!Layout.TryGetSlot(cardId, out int p, out _))
            {
                CardData card = context.Cards.Get(cardId);
                if (card == null || FullLayout.IndexOf(cardId) < 0) return false;
                tab = BinderTabs.ForElement(card.element);
                layout = null;
                if (!Layout.TryGetSlot(cardId, out p, out _)) return false;
            }
            ShowPage(p);
            return true;
        }

        void Refresh()
        {
            dirty = false;
            BinderLayout l = Layout;
            BinderLayout all = FullLayout;
            for (int i = 0; i < slots.Length; i++)
            {
                string id = l?.IdAt(page, i);
                CardData card = id != null ? context.Cards.Get(id) : null;
                bool discovered = card != null && collection != null && collection.IsDiscovered(id);
                int count = card != null && collection != null ? collection.CountOf(id) : 0;
                int number = all != null ? all.IndexOf(id) + 1 : 0; // 탭과 상관없이 도감 번호
                slots[i].Show(card, number, discovered, count, styles);
                slots[i].SetSelected(card != null && card == SelectedCard);
            }

            prevButton.interactable = l != null && l.HasPrevious(page);
            nextButton.interactable = l != null && l.HasNext(page);
            RefreshTabs();
            if (pageLabel != null)
            {
                int total = all != null ? all.CardCount : 0;
                int found = collection != null ? collection.DiscoveredCount : 0;
                string tabName = tab == BinderTab.All ? "" : $"{BinderTabs.Label(tab)}   ·   ";
                pageLabel.text = $"{tabName}{page + 1} / {PageCount} 쪽   ·   도감 {found} / {total}";
            }
        }

        // 고른 탭은 노란 굵은 글씨 + 밑줄, 오늘 인기 속성 탭에는 ★
        void RefreshTabs()
        {
            if (tabButtons == null) return;
            CardElement popular = PopularToday();
            for (int i = 0; i < tabButtons.Length && i < BinderTabs.Order.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                TMP_Text label = tabButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (label == null) continue;
                BinderTab t = BinderTabs.Order[i];
                bool on = t == tab;
                bool hot = popular != CardElement.None && BinderTabs.ToElement(t) == popular;
                label.text = hot ? $"★{BinderTabs.Label(t)}" : BinderTabs.Label(t);
                label.color = on ? tabSelectedText : Color.white;
                label.fontStyle = on ? FontStyles.Bold | FontStyles.Underline : FontStyles.Normal;
            }
        }

        CardElement PopularToday()
        {
            if (context == null || context.Session == null || context.PopularElements == null) return CardElement.None;
            return context.PopularToday;
        }

        // ------------------------------------------------------------------ 선택 (판매 등록 연결 지점)

        void OnSlotClicked(int slotIndex)
        {
            string id = Layout?.IdAt(page, slotIndex);
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

        // ------------------------------------------------------------------ 표지 소켓 → 칸으로 넣기

        void OnCardSocketed(SelectEnterEventArgs args)
        {
            var card = args.interactableObject.transform.GetComponent<CardRevealView>();
            if (card != null) StartCoroutine(StoreFromSocket(card));
        }

        IEnumerator StoreFromSocket(CardRevealView card)
        {
            // 선택 이벤트 처리 도중에 선택을 풀면 XRI 내부 순서가 꼬일 수 있어 한 프레임 뒤에 풉니다.
            storing++;
            coverSocket.socketActive = false; // 날아가는 동안 같은 카드가 다시 꽂히지 않게
            yield return null;
            if (coverSocket.hasSelection && coverSocket.interactionManager != null)
                coverSocket.interactionManager.CancelInteractorSelection((IXRSelectInteractor)coverSocket);

            yield return Store(card);

            storing--;
            if (storing <= 0)
            {
                storing = 0;
                coverSocket.socketActive = true;
            }
        }

        /// <summary>
        /// 물리 카드를 자기 칸으로 날려 보내고 사라지게 합니다(데이터는 그대로). 표지 소켓이 쓰고,
        /// 나중에 "보관함에 넣기"(시연 피드백 B2)도 같은 함수를 쓰면 됩니다.
        /// </summary>
        public void StoreCard(CardRevealView card)
        {
            if (card != null) StartCoroutine(Store(card));
        }

        IEnumerator Store(CardRevealView card)
        {
            CardData data = card.Card;
            card.SetGrabbable(false);
            if (data == null || !ShowCard(data.id))
            {
                card.Hide(); // 바인더에 칸이 없는 카드(데이터 오류)는 그냥 정리
                yield break;
            }
            Layout.TryGetSlot(data.id, out _, out int slotIndex);
            BinderSlotView slot = slots[slotIndex];

            // Transform과 Rigidbody를 함께 옮김 (BUG-003: 한쪽만 옮기면 다음 물리 스텝에 옛 위치로 튐)
            var body = card.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            Vector3 fromPos = card.transform.position;
            Quaternion fromRot = card.transform.rotation;
            Vector3 fromScale = card.transform.localScale;
            Vector3 toPos = slot.transform.position;
            Quaternion toRot = slot.transform.rotation; // 칸(UI)과 카드 모두 앞면이 -Z
            for (float t = 0f; t < 1f; t += Time.deltaTime / flySeconds)
            {
                float e = Mathf.SmoothStep(0f, 1f, t);
                Vector3 pos = Vector3.Lerp(fromPos, toPos, e) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.08f);
                Quaternion rot = Quaternion.Slerp(fromRot, toRot, e);
                card.transform.SetPositionAndRotation(pos, rot);
                if (body != null)
                {
                    body.position = pos;
                    body.rotation = rot;
                }
                card.transform.localScale = Vector3.Lerp(fromScale, fromScale * 0.6f, e);
                yield return null;
            }

            card.Hide(); // 풀(CardRevealer)로 돌아갈 카드라 지우지 않고 끕니다
            card.transform.localScale = fromScale;
            slot.Flash();
        }
    }
}
