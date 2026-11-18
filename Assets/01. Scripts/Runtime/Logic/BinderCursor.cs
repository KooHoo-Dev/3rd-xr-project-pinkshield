using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>
    /// 바인더에서 지금 보는 곳 (순수 C#, BinderCursorTests): 속성 탭 · 쪽 · 카드 찾아가기 · 도감 번호.
    /// BinderView에서 떼어 냄 (큰_클래스_나누기_제안 3장). 칸 배치 규칙은 BinderLayout, 탭 규칙은 BinderTabs.
    /// </summary>
    public sealed class BinderCursor
    {
        readonly IReadOnlyList<CardData> cards;
        readonly int perPage;
        BinderLayout layout;     // 지금 탭에 보이는 카드만 (탭이 바뀌면 다시 만듦)
        BinderLayout fullLayout; // 전체 카드 (도감 번호용)

        /// <param name="cardsInOrder">카드 DB 순서 그대로</param>
        /// <param name="slotsPerPage">한 쪽 칸 수</param>
        public BinderCursor(IReadOnlyList<CardData> cardsInOrder, int slotsPerPage)
        {
            cards = cardsInOrder;
            perPage = slotsPerPage;
        }

        public BinderTab Tab { get; private set; } = BinderTab.All;
        public int Page { get; private set; }

        public BinderLayout Layout => layout ??= new BinderLayout(BinderTabs.FilterIds(cards, Tab), perPage);
        public BinderLayout FullLayout => fullLayout ??= new BinderLayout(BinderTabs.FilterIds(cards, BinderTab.All), perPage);
        public int PageCount => Layout.PageCount;

        /// <summary>쪽을 옮김 (범위 밖이면 처음 · 끝으로).</summary>
        public void ShowPage(int index) => Page = Layout.ClampPage(index);

        /// <summary>속성 탭을 바꾸고 그 탭의 첫 쪽으로.</summary>
        public void ShowTab(BinderTab next)
        {
            if (next != Tab)
            {
                Tab = next;
                layout = null;
            }
            Page = 0;
        }

        /// <summary>새 게임: 전체 탭 첫 쪽.</summary>
        public void Reset() => ShowTab(BinderTab.All);

        /// <summary>
        /// 그 카드가 있는 쪽으로. 지금 탭에 없는 카드면 그 카드의 속성 탭으로 바꿈. 바인더에 없는 카드면 false (그대로).
        /// </summary>
        public bool ShowCard(CardData card)
        {
            if (card == null) return false;
            if (!Layout.TryGetSlot(card.id, out int p, out _))
            {
                if (FullLayout.IndexOf(card.id) < 0) return false;
                BinderTab before = Tab;
                Tab = BinderTabs.ForElement(card.element);
                layout = null;
                if (!Layout.TryGetSlot(card.id, out p, out _))
                {
                    Tab = before;
                    layout = null;
                    return false;
                }
            }
            Page = p;
            return true;
        }

        /// <summary>지금 쪽 slot번째 칸의 카드 id (빈 칸이면 null).</summary>
        public string IdAt(int slot) => Layout.IdAt(Page, slot);

        /// <summary>지금 탭에서 그 카드의 칸 번호 (없으면 -1).</summary>
        public int SlotOf(string id) => Layout.TryGetSlot(id, out _, out int slot) ? slot : -1;

        /// <summary>탭과 상관없는 도감 번호 (1부터, 없으면 0).</summary>
        public int NumberOf(string id) => FullLayout.IndexOf(id) + 1;

        public bool HasPrevious => Layout.HasPrevious(Page);
        public bool HasNext => Layout.HasNext(Page);
    }

    /// <summary>
    /// 퇴근 때 보관함으로 들어온 카드 (집에 도착하면 그 칸이 반짝임). 중복 없이 넣은 순서를 지킴 (순수 C#, BinderCursorTests).
    /// </summary>
    public sealed class StashedCards
    {
        readonly HashSet<string> set = new HashSet<string>();
        readonly List<string> order = new List<string>();

        public int Count => order.Count;
        public bool Contains(string id) => set.Contains(id);
        /// <summary>마지막에 넣은 카드 (없으면 null) — 그 쪽을 펼쳐 둠.</summary>
        public string Last => order.Count > 0 ? order[order.Count - 1] : null;

        public void Add(IEnumerable<string> ids)
        {
            foreach (string id in ids)
                if (id != null && set.Add(id)) order.Add(id);
        }

        public void Clear()
        {
            set.Clear();
            order.Clear();
        }
    }
}
