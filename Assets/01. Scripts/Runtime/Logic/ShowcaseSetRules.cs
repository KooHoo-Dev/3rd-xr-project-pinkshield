using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>
    /// 방 진열장 세트 보너스 (경제시스템_설계 14-3, DEC-040 U1): 같은 속성 슬라임 수퍼레어 이상 3장 = 세트 1개.
    /// 한 속성에서 6장이면 2세트. 세트 수는 최대 MaxSets. 순수 C# (ShowcaseSetRulesTests).
    /// </summary>
    public static class ShowcaseSetRules
    {
        public const int SetSize = 3;
        public const int MaxSets = 3;
        public const CardRarity MinRarity = CardRarity.SuperRare;

        /// <summary>세트가 된 속성 목록 (속성마다 세트 수만큼 반복, 많은 속성부터, 최대 MaxSets개).</summary>
        public static List<CardElement> Sets(IEnumerable<CardData> showcase)
        {
            var count = new Dictionary<CardElement, int>();
            var order = new List<CardElement>();
            if (showcase != null)
                foreach (CardData c in showcase)
                {
                    if (!Counts(c)) continue;
                    if (!count.ContainsKey(c.element)) { count[c.element] = 0; order.Add(c.element); }
                    count[c.element]++;
                }

            // 장수 많은 속성 먼저, 같으면 먼저 놓인 순 (결과가 늘 같도록)
            order.Sort((a, b) =>
            {
                int byCount = count[b].CompareTo(count[a]);
                return byCount != 0 ? byCount : ((int)a).CompareTo((int)b);
            });

            var sets = new List<CardElement>();
            foreach (CardElement e in order)
                for (int i = 0; i < count[e] / SetSize && sets.Count < MaxSets; i++) sets.Add(e);
            return sets;
        }

        public static int SetCount(IEnumerable<CardData> showcase) => Sets(showcase).Count;

        /// <summary>진열장 칸마다 세트에 들어간 카드인가 (같은 속성 앞에서부터 세트 수 × 3장). 세트 조명 · 반짝임용</summary>
        public static bool[] SetSlots(IList<CardData> showcase)
        {
            var inSet = new bool[showcase != null ? showcase.Count : 0];
            if (showcase == null) return inSet;
            var quota = new Dictionary<CardElement, int>();
            foreach (CardElement e in Sets(showcase))
                quota[e] = (quota.TryGetValue(e, out int q) ? q : 0) + SetSize;
            for (int i = 0; i < showcase.Count; i++)
            {
                CardData c = showcase[i];
                if (!Counts(c) || !quota.TryGetValue(c.element, out int left) || left <= 0) continue;
                inSet[i] = true;
                quota[c.element] = left - 1;
            }
            return inSet;
        }

        /// <summary>세트에 셀 수 있는 카드인가 (슬라임 · 수퍼레어 이상)</summary>
        public static bool Counts(CardData c) =>
            c != null && c.kind == CardKind.Slime && c.element != CardElement.None && c.rarity >= MinRarity;
    }
}
