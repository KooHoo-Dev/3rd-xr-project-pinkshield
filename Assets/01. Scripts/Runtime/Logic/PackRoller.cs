using System;
using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Logic
{
    public enum SpecialPackType
    {
        None = 0,
        GodPack = 1,       // 5장 모두 유니크 이상
        LegendaryPack = 2, // 히트 슬롯 레전더리 확정
    }

    /// <summary>팩 1개를 연 결과.</summary>
    public sealed class PackResult
    {
        public CardData[] Cards;
        public SpecialPackType Special;
        public PackClue Clue;
        public bool IsBoxPack;
        /// <summary>카드별 "처음 얻은 카드" 여부. PackOpeningService가 채웁니다.</summary>
        public bool[] IsNew;

        public CardRarity BestRarity
        {
            get
            {
                CardRarity best = CardRarity.Normal;
                foreach (CardData c in Cards) if (c.rarity > best) best = c.rarity;
                return best;
            }
        }
    }

    /// <summary>
    /// 팩 확률 판정 (기획서 5-3). 판정 순서: 갓팩 → 레전더리 확정 팩 → 슬롯별 판정. 등급이 정해지면 그 등급 카드 중 균등 추첨.
    /// 상자 팩은 단서 보정 없음(기획서 5-2). 특수 팩 판정은 상자 팩에도 적용(기획서 ④ "상자 1개에서" 수치).
    /// </summary>
    public sealed class PackRoller
    {
        readonly PackOddsConfig odds;
        readonly CardDatabase cards;
        readonly IRandomSource random;

        // 디버그 치트용 1회성 강제 값 (촬영용, 기획서 10장)
        SpecialPackType? forcedSpecial;
        CardRarity? forcedHitRarity;

        public PackRoller(PackOddsConfig odds, CardDatabase cards, IRandomSource random)
        {
            this.odds = odds;
            this.cards = cards;
            this.random = random;
        }

        /// <summary>다음 팩 1개의 특수 팩 종류를 강제합니다.</summary>
        public void ForceNextSpecial(SpecialPackType special) => forcedSpecial = special;

        /// <summary>다음 팩 1개의 히트 슬롯 등급을 강제합니다.</summary>
        public void ForceNextHitRarity(CardRarity rarity) => forcedHitRarity = rarity;

        public PackResult Roll(PackClue clue, bool isBoxPack)
        {
            int clueCount = isBoxPack ? 0 : CardEnumUtil.ClueCount(clue);
            SpecialPackType special = forcedSpecial ?? RollSpecial();
            CardRarity? hitOverride = forcedHitRarity;
            forcedSpecial = null;
            forcedHitRarity = null;

            PackSlotType[] layout = odds.slotLayout;
            var result = new PackResult
            {
                Cards = new CardData[layout.Length],
                Special = special,
                Clue = isBoxPack ? PackClue.None : clue,
                IsBoxPack = isBoxPack,
            };

            for (int i = 0; i < layout.Length; i++)
            {
                CardRarity rarity;
                if (special == SpecialPackType.GodPack)
                    rarity = random.NextDouble() * 100.0 < odds.godPackLegendaryShare ? CardRarity.Legendary : CardRarity.Unique;
                else if (layout[i] == PackSlotType.Hit && special == SpecialPackType.LegendaryPack)
                    rarity = CardRarity.Legendary;
                else if (layout[i] == PackSlotType.Hit && hitOverride.HasValue)
                    rarity = hitOverride.Value;
                else
                    rarity = PickRarity(odds.GetSlotWeights(layout[i], clueCount));

                result.Cards[i] = PickCard(rarity);
            }
            return result;
        }

        SpecialPackType RollSpecial()
        {
            double r = random.NextDouble() * 100.0;
            if (r < odds.godPackChance) return SpecialPackType.GodPack;
            if (r < odds.godPackChance + odds.legendaryPackChance) return SpecialPackType.LegendaryPack;
            return SpecialPackType.None;
        }

        CardRarity PickRarity(RarityWeights w)
        {
            double r = random.NextDouble() * w.Sum;
            for (int i = 0; i < CardEnumUtil.RarityCount; i++)
            {
                var rarity = (CardRarity)i;
                r -= w[rarity];
                if (r < 0) return rarity;
            }
            // 부동소수 오차로 끝까지 온 경우: 확률이 0이 아닌 가장 높은 등급
            for (int i = CardEnumUtil.RarityCount - 1; i >= 0; i--)
                if (w[(CardRarity)i] > 0f) return (CardRarity)i;
            return CardRarity.Normal;
        }

        CardData PickCard(CardRarity rarity)
        {
            IReadOnlyList<CardData> pool = cards.GetByRarity(rarity);
            if (pool.Count == 0)
                throw new InvalidOperationException($"{rarity} 등급 카드가 CardDatabase에 없습니다. Tools > CardShop > Import Cards CSV 를 실행하세요.");
            return pool[random.Range(0, pool.Count)];
        }
    }

    /// <summary>선반 팩의 단서 배치 (기획서 5-2: 일반 75 / 단서 1종 20 / 단서 2종 5).</summary>
    public static class ShelfGenerator
    {
        public static PackClue[] Generate(int slotCount, PackOddsConfig odds, IRandomSource random)
        {
            var clues = new PackClue[slotCount];
            float total = odds.shelfNoClue + odds.shelfOneClue + odds.shelfTwoClues;
            for (int i = 0; i < slotCount; i++)
            {
                double r = random.NextDouble() * total;
                if (r < odds.shelfNoClue) clues[i] = PackClue.None;
                else if (r < odds.shelfNoClue + odds.shelfOneClue)
                    clues[i] = random.Range(0, 2) == 0 ? PackClue.FoldedTop : PackClue.FlippedBack;
                else clues[i] = PackClue.FoldedTop | PackClue.FlippedBack;
            }
            return clues;
        }
    }
}
