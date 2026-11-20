using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>
    /// 하루의 카드 시세 (경제시스템_설계 6 · 7장, DEC-041): 1순위 · 2순위 인기 속성과 각 배율, 강도 이름.
    /// 인기 속성은 슬라임 카드에만 적용됩니다(EconomyRules가 판단).
    /// </summary>
    public readonly struct MarketDay
    {
        public static readonly MarketDay None = new MarketDay(CardElement.None, 1f, CardElement.None, 1f, -1, "");

        public CardElement Primary { get; }
        public float PrimaryMultiplier { get; }
        public CardElement Secondary { get; }
        public float SecondaryMultiplier { get; }
        /// <summary>강도 등급 번호 (EconomyConfig.popularTiers, 0 = 가장 약함). 인기 없음이면 −1</summary>
        public int TierIndex { get; }
        /// <summary>"소폭 상승" · "상승" · "급등" · "폭등"</summary>
        public string TierName { get; }

        public MarketDay(CardElement primary, float primaryMultiplier, CardElement secondary, float secondaryMultiplier, int tierIndex, string tierName)
        {
            Primary = primary;
            PrimaryMultiplier = primaryMultiplier;
            Secondary = secondary;
            SecondaryMultiplier = secondaryMultiplier;
            TierIndex = tierIndex;
            TierName = tierName ?? "";
        }

        public bool HasPopular => Primary != CardElement.None;

        /// <summary>속성의 순위: 1 = 1순위, 2 = 2순위, 0 = 인기 아님</summary>
        public int RankOf(CardElement element)
        {
            if (element == CardElement.None) return 0;
            if (element == Primary) return 1;
            if (element == Secondary) return 2;
            return 0;
        }

        /// <summary>속성의 가격 배율 (인기 아니면 1)</summary>
        public float MultiplierOf(CardElement element)
        {
            switch (RankOf(element))
            {
                case 1: return PrimaryMultiplier;
                case 2: return SecondaryMultiplier;
                default: return 1f;
            }
        }
    }

    /// <summary>
    /// 일차별 시세 일정 (경제시스템_설계 6 · 7장). 게임 시작 때 공식 기간을 미리 뽑아 둡니다(같은 시드면 같은 일정).
    /// - 주기마다 서로 다른 속성 2개(1순위 · 2순위). 바로 앞 주기의 1순위는 다시 1순위가 되지 않음.
    /// - 1순위 강도는 등급표에서 가중치로 뽑고, 2순위 배율 = 1 + (1순위 − 1) × secondaryBonusShare.
    /// - 무한 모드로 공식 기간을 넘으면 주기 길이가 endlessCycleDays 사이에서 무작위.
    /// </summary>
    public sealed class MarketSchedule
    {
        static readonly CardElement[] SlimeElements =
            { CardElement.Fire, CardElement.Water, CardElement.Earth, CardElement.Air, CardElement.Neutral };

        readonly List<MarketDay> byDay = new List<MarketDay>();
        readonly int officialDays;
        readonly EconomyConfig config;
        readonly IRandomSource random;
        CardElement lastPrimary = CardElement.None;

        public MarketSchedule(int officialDays, EconomyConfig config, IRandomSource random)
        {
            this.officialDays = officialDays;
            this.config = config;
            this.random = random;
            Extend(officialDays);
        }

        /// <summary>1부터 시작하는 일차의 시세. 0 이하는 None, 처음 일정보다 뒤면 이어서 뽑음.</summary>
        public MarketDay For(int day)
        {
            if (day < 1 || day > 10000) return MarketDay.None;
            Extend(day);
            return byDay[day - 1];
        }

        void Extend(int days)
        {
            while (byDay.Count < days)
            {
                MarketDay cycle = RollCycle();
                int length = CycleLength(byDay.Count);
                for (int i = 0; i < length; i++) byDay.Add(cycle);
            }
        }

        // 공식 기간은 고정 주기, 그 뒤(무한 모드)는 최소~최대 사이 무작위
        int CycleLength(int daysSoFar)
        {
            if (daysSoFar < officialDays) return config.popularCycleDays < 1 ? 1 : config.popularCycleDays;
            int min = config.endlessCycleDays.x < 1 ? 1 : config.endlessCycleDays.x;
            int max = config.endlessCycleDays.y < min ? min : config.endlessCycleDays.y;
            return random.Range(min, max + 1);
        }

        MarketDay RollCycle()
        {
            CardElement primary;
            do primary = SlimeElements[random.Range(0, SlimeElements.Length)];
            while (primary == lastPrimary);
            lastPrimary = primary;

            CardElement secondary;
            do secondary = SlimeElements[random.Range(0, SlimeElements.Length)];
            while (secondary == primary);

            int tier = PickTier();
            float mult = tier >= 0 ? config.popularTiers[tier].multiplier : 1.5f;
            string name = tier >= 0 ? config.popularTiers[tier].name : "상승";
            float second = 1f + (mult - 1f) * config.secondaryBonusShare;
            return new MarketDay(primary, mult, secondary, second, tier, name);
        }

        int PickTier()
        {
            EconomyConfig.PopularTier[] tiers = config.popularTiers;
            if (tiers == null || tiers.Length == 0) return -1;
            float sum = 0f;
            foreach (EconomyConfig.PopularTier t in tiers) sum += t.weight > 0f ? t.weight : 0f;
            if (sum <= 0f) return 0;
            double r = random.NextDouble() * sum;
            for (int i = 0; i < tiers.Length; i++)
            {
                r -= tiers[i].weight > 0f ? tiers[i].weight : 0f;
                if (r < 0) return i;
            }
            return tiers.Length - 1;
        }
    }
}
