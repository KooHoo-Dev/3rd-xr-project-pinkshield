using System;
using System.Collections.Generic;

namespace CardShop.Logic
{
    /// <summary>
    /// 바인더 칸 배치 규칙 (순수 C#). 카드 목록(CardDatabase 순서)을 한 페이지 N칸씩 나눠
    /// "카드 ID → (페이지, 칸)"을 정합니다. 카드 수·페이지 수는 데이터에서 계산하므로 코드에 숫자가 없습니다.
    /// 칸 위치만 정할 뿐 보유 장수는 모릅니다(보유는 GameSession.Collection이 진실).
    /// </summary>
    public sealed class BinderLayout
    {
        readonly List<string> ids = new List<string>();
        readonly Dictionary<string, int> indexById = new Dictionary<string, int>();

        /// <param name="cardIdsInOrder">카드 ID 순서 (보통 CardDatabase.cards 순서). 빈 ID·중복 ID는 건너뜀</param>
        /// <param name="slotsPerPage">한 페이지 칸 수 (1 이상)</param>
        public BinderLayout(IEnumerable<string> cardIdsInOrder, int slotsPerPage)
        {
            if (slotsPerPage <= 0) throw new ArgumentOutOfRangeException(nameof(slotsPerPage));
            SlotsPerPage = slotsPerPage;
            if (cardIdsInOrder == null) return;
            foreach (string id in cardIdsInOrder)
            {
                if (string.IsNullOrEmpty(id) || indexById.ContainsKey(id)) continue;
                indexById[id] = ids.Count;
                ids.Add(id);
            }
        }

        public int SlotsPerPage { get; }
        public int CardCount => ids.Count;

        /// <summary>페이지 수. 카드가 없어도 빈 페이지 1장은 보여 줍니다.</summary>
        public int PageCount => Math.Max(1, (ids.Count + SlotsPerPage - 1) / SlotsPerPage);

        /// <summary>바인더 전체에서 몇 번째 칸인지 (0부터). 없는 카드면 -1.</summary>
        public int IndexOf(string id) => id != null && indexById.TryGetValue(id, out int i) ? i : -1;

        /// <summary>카드가 들어갈 페이지와 칸 (둘 다 0부터).</summary>
        public bool TryGetSlot(string id, out int page, out int slot)
        {
            int i = IndexOf(id);
            if (i < 0)
            {
                page = slot = -1;
                return false;
            }
            page = i / SlotsPerPage;
            slot = i % SlotsPerPage;
            return true;
        }

        /// <summary>그 칸의 카드 ID. 마지막 페이지의 남는 칸이나 범위 밖이면 null.</summary>
        public string IdAt(int page, int slot)
        {
            if (page < 0 || slot < 0 || slot >= SlotsPerPage) return null;
            int i = page * SlotsPerPage + slot;
            return i < ids.Count ? ids[i] : null;
        }

        /// <summary>페이지 번호를 0 ~ 마지막 페이지 사이로 맞춥니다.</summary>
        public int ClampPage(int page)
        {
            if (page < 0) return 0;
            int last = PageCount - 1;
            return page > last ? last : page;
        }

        public bool HasPrevious(int page) => page > 0;
        public bool HasNext(int page) => page < PageCount - 1;
    }
}
