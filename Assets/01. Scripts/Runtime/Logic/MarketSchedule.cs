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
        /// <summary>하락 속성 (무한 모드 연장 4일부터, 없으면 None) · 배율</summary>
        public CardElement Falling { get; }
        public float FallingMultiplier { get; }

        public MarketDay(CardElement primary, float primaryMultiplier, CardElement secondary, float secondaryMultiplier, int tierIndex, string tierName,
            CardElement falling = CardElement.None, float fallingMultiplier = 1f)
        {
            Falling = falling;
            FallingMultiplier = fallingMultiplier;
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

        public bool IsFalling(CardElement element) => element != CardElement.None && element == Falling;

        /// <summary>속성의 가격 배율 (인기 아니면 1, 하락 속성이면 FallingMultiplier)</summary>
        public float MultiplierOf(CardElement element)
        {
            switch (RankOf(element))
            {
                case 1: return PrimaryMultiplier;
                case 2: return SecondaryMultiplier;
                default: return IsFalling(element) ? FallingMultiplier : 1f;
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

        /// <summary>
        /// 개발용 (디버그 패널 "시세 강도 올리기", 11-21 보완 B5): 그 날의 1순위 강도를 한 단계 올립니다(폭등 다음은 처음 단계로).
        /// 속성 · 하락은 그대로, 2순위 배율은 같은 식으로 다시 계산. 같은 주기의 다른 날은 바꾸지 않습니다.
        /// </summary>
        public MarketDay DebugNextTier(int day)
        {
            MarketDay d = For(day);
            EconomyConfig.PopularTier[] tiers = config.popularTiers;
            if (!d.HasPopular || tiers == null || tiers.Length == 0) return d;
            int tier = (d.TierIndex + 1) % tiers.Length;
            float mult = tiers[tier].multiplier;
            var next = new MarketDay(d.Primary, mult, d.Secondary, 1f + (mult - 1f) * config.secondaryBonusShare, tier, tiers[tier].name,
                d.Falling, d.FallingMultiplier);
            byDay[day - 1] = next;
            return next;
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
            int extraDay = byDay.Count + 1 - officialDays; // 이 주기가 시작하는 연장 날 (공식 기간이면 0 이하)
            CardElement primary;
            do primary = SlimeElements[random.Range(0, SlimeElements.Length)];
            while (primary == lastPrimary);
            lastPrimary = primary;

            CardElement secondary;
            do secondary = SlimeElements[random.Range(0, SlimeElements.Length)];
            while (secondary == primary);

            List<EconomyConfig.PopularTier> tiers = TiersFor(extraDay);
            int tier = PickTier(tiers);
            float mult = tier >= 0 ? tiers[tier].multiplier : 1.5f;
            string name = tier >= 0 ? tiers[tier].name : "상승";
            float second = 1f + (mult - 1f) * config.secondaryBonusShare;

            // 무한 모드 연장 4일부터: 인기 2개와 다른 속성 하나가 하락 (7-1)
            CardElement falling = CardElement.None;
            if (extraDay >= config.endlessWildFromExtraDay && config.endlessWildFromExtraDay > 0)
                do falling = SlimeElements[random.Range(0, SlimeElements.Length)];
                while (falling == primary || falling == secondary);
            return new MarketDay(primary, mult, secondary, second, tier, name, falling, config.fallingMultiplier);
        }

        // 강도 표: 공식 기간은 설정 그대로. 연장 날은 3일마다 소폭 → 폭등으로 가중치를 옮기고, 연장 4일부터 "역대급" 추가 (7-1)
        List<EconomyConfig.PopularTier> TiersFor(int extraDay)
        {
            var list = new List<EconomyConfig.PopularTier>();
            if (config.popularTiers != null)
                foreach (EconomyConfig.PopularTier t in config.popularTiers)
                    list.Add(new EconomyConfig.PopularTier { name = t.name, multiplier = t.multiplier, weight = t.weight });
            if (extraDay <= 0 || list.Count < 2) return list;

            float shift = System.Math.Min(config.endlessBoomShiftMax, config.endlessBoomShiftPer3Days * (extraDay / 3));
            shift = System.Math.Min(shift, list[0].weight);
            list[0].weight -= shift;
            list[list.Count - 1].weight += shift;
            if (extraDay >= config.endlessWildFromExtraDay && config.endlessWildTier != null && config.endlessWildTier.weight > 0f)
                list.Add(config.endlessWildTier);
            return list;
        }

        int PickTier(List<EconomyConfig.PopularTier> tiers)
        {
            if (tiers.Count == 0) return -1;
            float sum = 0f;
            foreach (EconomyConfig.PopularTier t in tiers) sum += t.weight > 0f ? t.weight : 0f;
            if (sum <= 0f) return 0;
            double r = random.NextDouble() * sum;
            for (int i = 0; i < tiers.Count; i++)
            {
                r -= tiers[i].weight > 0f ? tiers[i].weight : 0f;
                if (r < 0) return i;
            }
            return tiers.Count - 1;
        }
    }
}
