using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>
    /// 밸런스 시뮬레이터 (경제시스템_설계 10장 E5, 시스템설계서 v1 "밸런스 시뮬레이터"): 실제 규칙 코드(팩 · 판매 · 시세 · 업그레이드 · 점수)로
    /// 7일 한 판을 여러 번 돌려 최종 점수 분포를 봅니다. 손님 · 청소 · 계산은 평균값으로 단순화합니다(순수 C#).
    /// 플레이어 모델: 매일 남은 돈으로 팩을 다 사고, 레어 이하는 모두 팔기, 수퍼레어 이상은 내일 기대값 순으로 판매 등록,
    /// 남는 카드는 같은 속성끼리 진열장 세트로. 가구는 정책에 적힌 날 밤에 삼.
    /// </summary>
    public sealed class EconomySimulator
    {
        /// <summary>가구 구매 정책: (그날 밤 일차, 분야)</summary>
        public sealed class Policy
        {
            public string Name;
            public readonly List<KeyValuePair<int, UpgradeKind>> Buys = new List<KeyValuePair<int, UpgradeKind>>();
            public Policy(string name) => Name = name;
            public Policy Buy(int day, UpgradeKind kind) { Buys.Add(new KeyValuePair<int, UpgradeKind>(day, kind)); return this; }
        }

        public sealed class Result
        {
            public string Policy;
            public readonly List<int> Scores = new List<int>();
            public readonly List<int> FurnitureSpent = new List<int>();
            public int Percentile(float p)
            {
                if (Scores.Count == 0) return 0;
                var sorted = new List<int>(Scores);
                sorted.Sort();
                int i = (int)(p * (sorted.Count - 1));
                return sorted[i];
            }
        }

        // 손님 외 하루 고정 수입 (기본급 + 청소 2곳 + 빠른 계산 절반) — 평균값 단순화
        public int ExtraDailyIncome = 15;

        readonly EconomyConfig economy;
        readonly RarityTable rarity;
        readonly PackOddsConfig odds;
        readonly CardDatabase cards;
        readonly CampaignConfig campaign;
        readonly ScoreConfig score;
        readonly UpgradeCatalog catalog;

        public EconomySimulator(EconomyConfig economy, RarityTable rarity, PackOddsConfig odds, CardDatabase cards,
            CampaignConfig campaign, ScoreConfig score, UpgradeCatalog catalog)
        {
            this.economy = economy; this.rarity = rarity; this.odds = odds; this.cards = cards;
            this.campaign = campaign; this.score = score; this.catalog = catalog;
        }

        public Result Run(Policy policy, int runs, int seed)
        {
            var result = new Result { Policy = policy.Name };
            for (int i = 0; i < runs; i++)
            {
                FinalResult r = RunOnce(policy, new SystemRandomSource(seed + i), out int spent);
                result.Scores.Add(r.Score);
                result.FurnitureSpent.Add(spent);
            }
            return result;
        }

        public FinalResult RunOnce(Policy policy, IRandomSource random, out int furnitureSpent)
        {
            var prices = new EconomyRules(economy, rarity);
            var upgrades = new UpgradeRules(catalog);
            var sales = new SalesRules(economy, rarity, prices, cards, random, upgrades);
            var roller = new PackRoller(odds, cards, random);
            var market = new MarketSchedule(campaign.DayCount, economy, random);
            var s = new GameSession(economy.startingMoney);

            for (int day = 1; day <= campaign.DayCount; day++)
            {
                MarketDay today = market.For(day);
                CampaignConfig.DayEntry entry = campaign.GetDay(day);

                // 영업: 손님마다 진열 카드를 둘러보고 1장까지
                int customers = random.Range(entry.customerMin, entry.customerMax + 1);
                for (int c = 0; c < customers; c++)
                {
                    CardData picked = sales.PickPurchase(s, today);
                    sales.CompleteSale(s, picked, today, out _);
                }
                s.Ledger.basePay = economy.dailyBasePay;
                s.Wallet.Add(prices.SettlementTotal(s.Ledger) + ExtraDailyIncome);

                // 퇴근 후: 무료 팩 + 남은 돈으로 팩 (가구 살 돈은 남겨 둠)
                int reserve = ReserveFor(policy, day, s.Upgrades, upgrades);
                int packs = economy.freePacksPerDay + upgrades.ExtraFreePacks(s.Upgrades);
                while (s.Wallet.Money - reserve >= economy.packPrice && s.Wallet.TrySpend(economy.packPrice)) packs++;
                for (int p = 0; p < packs; p++)
                    foreach (CardData card in roller.Roll(PackClue.None, false).Cards)
                        if (card != null) s.Collection.Add(card.id);

                if (day == campaign.DayCount) break; // 마지막 날은 집을 건너뜀

                // 집: 레어 이하 모두 팔기 → 가구 → 진열장 세트 → 판매 등록
                sales.SellAllUpTo(s, CardRarity.Rare, out _);
                foreach (KeyValuePair<int, UpgradeKind> b in policy.Buys)
                    if (b.Key == day && upgrades.TryBuy(s, b.Value, out _) == BuyResult.Ok) s.Upgrades.Open(b.Value);
                FillShowcase(s, upgrades);
                FillDisplay(s, sales, prices, market.For(day + 1));
                s.AdvanceDay();
            }

            sales.ReturnUnsold(s);
            furnitureSpent = s.Upgrades.Spent;
            return new ScoreRules(score, rarity, cards).Calculate(s);
        }

        int ReserveFor(Policy policy, int day, UpgradeState st, UpgradeRules upgrades)
        {
            int sum = 0;
            foreach (KeyValuePair<int, UpgradeKind> b in policy.Buys)
                if (b.Key == day) sum += upgrades.NextLevel(st, b.Value)?.price ?? 0;
            return sum;
        }

        // 같은 속성 수퍼레어 이상이 3장 모이면 진열장에 세트로 세움 (칸이 있는 만큼, 싼 카드부터)
        void FillShowcase(GameSession s, UpgradeRules upgrades)
        {
            int free = upgrades.ShowcaseSlots(s.Upgrades) - s.Showcase.Count;
            var byElement = new Dictionary<CardElement, List<CardData>>();
            foreach (KeyValuePair<string, int> e in s.Collection.Owned)
            {
                CardData c = cards.Get(e.Key);
                if (!ShowcaseSetRules.Counts(c)) continue;
                if (!byElement.TryGetValue(c.element, out List<CardData> list)) byElement[c.element] = list = new List<CardData>();
                for (int i = 0; i < e.Value; i++) list.Add(c);
            }
            foreach (List<CardData> list in byElement.Values)
            {
                if (list.Count < ShowcaseSetRules.SetSize || free < ShowcaseSetRules.SetSize) continue;
                list.Sort((a, b) => a.rarity.CompareTo(b.rarity));
                for (int i = 0; i < ShowcaseSetRules.SetSize; i++)
                    if (ShowcaseRules.TryAdd(s, list[i].id, upgrades.ShowcaseSlots(s.Upgrades)) == ShowcaseResult.Ok) free--;
            }
        }

        // 내일 기대 수입(가게 값 × 구매 확률)이 큰 카드부터 판매 등록
        void FillDisplay(GameSession s, SalesRules sales, EconomyRules prices, MarketDay tomorrow)
        {
            var candidates = new List<CardData>();
            foreach (KeyValuePair<string, int> e in s.Collection.Owned)
            {
                CardData c = cards.Get(e.Key);
                if (c == null || !sales.CanDisplay(c)) continue;
                for (int i = 0; i < e.Value; i++) candidates.Add(c);
            }
            candidates.Sort((a, b) =>
                (prices.SellPrice(b, tomorrow) * sales.BuyChance(b, tomorrow)).CompareTo(prices.SellPrice(a, tomorrow) * sales.BuyChance(a, tomorrow)));
            foreach (CardData c in candidates)
            {
                if (!sales.HasFreeSlot(s)) break;
                sales.Display(s, c.id);
            }
        }
    }
}
