using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>바인더 정렬 (11-20 플레이 피드백 F1, Q4: 둘만).</summary>
    public enum BinderSortOrder
    {
        Dex = 0,     // 도감순 (카드 DB 순서)
        Rarity = 1,  // 레어도순: 높은 등급부터, 같은 등급 안에서는 도감순
    }

    /// <summary>
    /// 바인더 정렬 규칙 (순수 C#, BinderSortTests). 속성 탭으로 거른 카드를 정렬해 BinderLayout에 넘길 ID 목록을 만듭니다.
    /// 도감 번호(칸에 쓰는 번호)는 정렬과 상관없이 도감순 그대로입니다 (BinderCursor.NumberOf).
    /// </summary>
    public static class BinderSort
    {
        /// <summary>누를 때마다 도감순 ⇄ 레어도순.</summary>
        public static BinderSortOrder Next(BinderSortOrder order) =>
            order == BinderSortOrder.Dex ? BinderSortOrder.Rarity : BinderSortOrder.Dex;

        /// <summary>버튼 글자 = 지금 정렬.</summary>
        public static string Label(BinderSortOrder order) => order == BinderSortOrder.Rarity ? "레어도순" : "도감순";

        /// <summary>탭에 보일 카드 ID를 정렬해서. 비어 있는(null) 카드는 뺍니다.</summary>
        public static List<string> Ids(IEnumerable<CardData> cardsInOrder, BinderTab tab, BinderSortOrder order)
        {
            var picked = new List<(CardData card, int dex)>();
            if (cardsInOrder != null)
            {
                int dex = 0;
                foreach (CardData c in cardsInOrder)
                {
                    if (c == null) continue;
                    if (BinderTabs.Contains(tab, c.element)) picked.Add((c, dex));
                    dex++;
                }
            }
            if (order == BinderSortOrder.Rarity)
                picked.Sort((a, b) =>
                {
                    int byRarity = ((int)b.card.rarity).CompareTo((int)a.card.rarity); // 높은 등급 먼저
                    return byRarity != 0 ? byRarity : a.dex.CompareTo(b.dex);      // 같으면 도감순 (List.Sort는 안정 정렬이 아니므로 직접)
                });

            var ids = new List<string>(picked.Count);
            foreach ((CardData card, int _) in picked) ids.Add(card.id);
            return ids;
        }
    }
}
