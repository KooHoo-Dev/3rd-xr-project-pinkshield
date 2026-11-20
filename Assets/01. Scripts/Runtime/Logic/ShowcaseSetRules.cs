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

        /// <summary>세트에 셀 수 있는 카드인가 (슬라임 · 수퍼레어 이상)</summary>
        public static bool Counts(CardData c) =>
            c != null && c.kind == CardKind.Slime && c.element != CardElement.None && c.rarity >= MinRarity;
    }
}
