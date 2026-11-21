using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>정해 둔 값을 차례로 돌려주는 난수 (판정 경계를 정확히 시험할 때).</summary>
    sealed class ScriptedRandom : IRandomSource
    {
        readonly Queue<double> doubles = new Queue<double>();
        readonly Queue<int> ints = new Queue<int>();

        public ScriptedRandom Double(params double[] v) { foreach (double d in v) doubles.Enqueue(d); return this; }
        public ScriptedRandom Int(params int[] v) { foreach (int i in v) ints.Enqueue(i); return this; }

        public double NextDouble() => doubles.Dequeue();
        public int Range(int minInclusive, int maxExclusive) => ints.Dequeue();
    }

    /// <summary>테스트용 작은 카드 DB와 설정 묶음.</summary>
    sealed class Fixture
    {
        public readonly EconomyConfig Economy = ScriptableObject.CreateInstance<EconomyConfig>();
        public readonly RarityTable Rarity = ScriptableObject.CreateInstance<RarityTable>();
        public readonly ScoreConfig Score = ScriptableObject.CreateInstance<ScoreConfig>();
        public readonly CardDatabase Db = ScriptableObject.CreateInstance<CardDatabase>();

        public Fixture()
        {
            Add("F-N1", CardKind.Slime, CardElement.Fire, CardRarity.Normal);
            Add("W-R1", CardKind.Slime, CardElement.Water, CardRarity.Rare);
            Add("F-SR", CardKind.Slime, CardElement.Fire, CardRarity.SuperRare);
            Add("F-U", CardKind.Slime, CardElement.Fire, CardRarity.Unique);
            Add("SN-L", CardKind.Snack, CardElement.None, CardRarity.Legendary);
        }

        void Add(string id, CardKind kind, CardElement element, CardRarity rarity)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.id = id; c.kind = kind; c.element = element; c.rarity = rarity;
            Db.cards.Add(c);
        }

        /// <summary>1순위만 인기인 시세 (2순위 없음)</summary>
        public static MarketDay Hot(CardElement primary, float multiplier = 1.5f) =>
            new MarketDay(primary, multiplier, CardElement.None, 1f, 1, "상승");

        public SalesRules Sales(IRandomSource rng) =>
            new SalesRules(Economy, Rarity, new EconomyRules(Economy, Rarity), Db, rng);

        public void Destroy()
        {
            foreach (CardData c in Db.cards) Object.DestroyImmediate(c);
            Object.DestroyImmediate(Db);
            Object.DestroyImmediate(Economy);
            Object.DestroyImmediate(Rarity);
            Object.DestroyImmediate(Score);
        }
    }

    public class SalesRulesTests
    {
        Fixture f;
        GameSession s;

        [SetUp]
        public void SetUp()
        {
            f = new Fixture();
            s = new GameSession(0);
        }

        [TearDown] public void TearDown() => f.Destroy();

        // 손님 한 명: 고르기(PickPurchase) + 결제(CompleteSale). 진열 카드를 샀으면 그 카드, 가게 재고면 null
        CardData Serve(SalesRules sales, MarketDay market)
        {
            CardData card = sales.PickPurchase(s, market);
            return sales.CompleteSale(s, card, market, out _) ? card : null;
        }

        [Test]
        public void Display_TakesCardOutOfCollection_AndKeepsDexEntry()
        {
            s.Collection.Add("F-SR");
            Assert.AreEqual(DisplayResult.Ok, f.Sales(new ScriptedRandom()).Display(s, "F-SR"));
            Assert.AreEqual(0, s.Collection.CountOf("F-SR"));
            Assert.IsTrue(s.Collection.IsDiscovered("F-SR"), "진열해도 도감은 유지");
            CollectionAssert.AreEqual(new[] { "F-SR" }, s.SalesDisplay);
        }

        [Test]
        public void Display_RejectsUnownedCard_AndFullDisplay()
        {
            SalesRules sales = f.Sales(new ScriptedRandom());
            Assert.AreEqual(DisplayResult.NotOwned, sales.Display(s, "F-SR"), "보유하지 않은 카드");

            for (int i = 0; i < 5; i++) s.Collection.Add("F-SR");
            for (int i = 0; i < f.Economy.salesDisplaySlots; i++) Assert.AreEqual(DisplayResult.Ok, sales.Display(s, "F-SR"));
            Assert.AreEqual(DisplayResult.DisplayFull, sales.Display(s, "F-SR"), "진열대 4칸이 다 참");
            Assert.AreEqual(1, s.Collection.CountOf("F-SR"), "실패하면 보유 목록에서 빠지지 않음");
        }

        [Test]
        public void Display_RejectsBelowSuperRare_AllowsSuperRareAndAbove()
        {
            SalesRules sales = f.Sales(new ScriptedRandom());
            foreach (string id in new[] { "F-N1", "W-R1", "F-SR", "F-U", "SN-L" }) s.Collection.Add(id);

            Assert.AreEqual(DisplayResult.RarityTooLow, sales.Display(s, "F-N1"));
            Assert.AreEqual(DisplayResult.RarityTooLow, sales.Display(s, "W-R1"));
            Assert.AreEqual(1, s.Collection.CountOf("F-N1"), "거부되면 보유 목록 그대로");
            Assert.AreEqual(DisplayResult.Ok, sales.Display(s, "F-SR"));
            Assert.AreEqual(DisplayResult.Ok, sales.Display(s, "F-U"));
            Assert.AreEqual(DisplayResult.Ok, sales.Display(s, "SN-L"));
            Assert.AreEqual(DisplayResult.NotOwned, sales.Display(s, "F-SR"), "이미 진열해 0장");

            f.Economy.minDisplayRarity = CardRarity.Rare; // 데이터로 바꾸면 규칙이 따라감
            Assert.IsTrue(sales.CanDisplay(f.Db.Get("W-R1")));
            Assert.IsFalse(sales.CanDisplay(f.Db.Get("F-N1")));
        }

        // 바코드 계산: 고르기만 해서는 진열대 · 장부가 바뀌지 않고, 결제 완료 때 기록됨
        [Test]
        public void PickPurchase_DoesNotChangeState_UntilCompleteSale()
        {
            s.SalesDisplay.Add("F-SR");
            SalesRules sales = f.Sales(new ScriptedRandom().Double(0.5, 0.0)); // 둘러보기 순서 1번, 구매 판정 1번

            CardData picked = sales.PickPurchase(s, MarketDay.None);
            Assert.AreEqual("F-SR", picked.id);
            CollectionAssert.AreEqual(new[] { "F-SR" }, s.SalesDisplay, "고르기만 해서는 진열대 그대로");
            Assert.AreEqual(0, s.Ledger.customersServed);
            Assert.IsEmpty(s.Ledger.sales);

            Assert.IsTrue(sales.CompleteSale(s, picked, MarketDay.None, out int net));
            Assert.IsEmpty(s.SalesDisplay);
            Assert.AreEqual(1, s.Ledger.customersServed);
            Assert.AreEqual(1, s.Ledger.sales.Count);
            Assert.AreEqual(s.Ledger.sales[0].net, net, "판매 알림 금액 = 장부에 적힌 수령액");
        }

        [Test]
        public void CompleteSale_StockPack_CountsCustomer_NoSale()
        {
            SalesRules sales = f.Sales(new ScriptedRandom());
            Assert.IsFalse(sales.CompleteSale(s, null, Fixture.Hot(CardElement.Fire), out _));
            Assert.AreEqual(1, s.Ledger.customersServed);
            Assert.IsEmpty(s.Ledger.sales);
        }

        // 매입가 표 (경제시스템_설계 5장, DEC-041): 노멀 0.2(낱장 0) · 레어 1 · 수퍼레어 5 · 유니크 25 · 레전더리 100
        [TestCase("F-N1", 0)]
        [TestCase("W-R1", 1)]
        [TestCase("F-SR", 5)]
        [TestCase("F-U", 25)]
        [TestCase("SN-L", 100)]
        public void InstantPrice_IsBuybackPrice_NoCommissionNoPopularBonus(string id, int expected)
        {
            Assert.AreEqual(expected, f.Sales(new ScriptedRandom()).InstantPrice(f.Db.Get(id)));
        }

        [Test]
        public void SellInstantly_PaysWallet_RemovesOneCard_KeepsDexEntry_NoLedger()
        {
            s.Collection.Add("F-N1");
            s.Collection.Add("F-N1");
            SalesRules sales = f.Sales(new ScriptedRandom());

            Assert.IsFalse(sales.TrySellInstantly(s, "F-N1", out int price), "노멀은 낱장 매입가 0 → 모두 팔기로만");
            Assert.AreEqual(2, s.Collection.CountOf("F-N1"), "못 팔면 그대로");

            s.Collection.Add("W-R1");
            Assert.IsTrue(sales.TrySellInstantly(s, "W-R1", out price));
            Assert.AreEqual(1, price);
            Assert.AreEqual(1, s.Wallet.Money);
            Assert.AreEqual(0, s.Collection.CountOf("W-R1"), "1장만 빠짐");
            Assert.AreEqual(0, s.Ledger.sales.Count, "정산에 합산하지 않음");

            s.Collection.Add("SN-L");
            Assert.IsTrue(sales.TrySellInstantly(s, "SN-L", out price));
            Assert.AreEqual(100, price);
            Assert.AreEqual(101, s.Wallet.Money);
            Assert.AreEqual(0, s.Collection.CountOf("SN-L"));
            Assert.IsTrue(s.Collection.IsDiscovered("SN-L"), "팔아도 도감 유지");
        }

        [Test]
        public void SellInstantly_RejectsUnownedAndUnknownCard()
        {
            SalesRules sales = f.Sales(new ScriptedRandom());
            Assert.IsFalse(sales.TrySellInstantly(s, "F-SR", out int price), "보유하지 않은 카드");
            Assert.AreEqual(0, price);
            Assert.IsFalse(sales.TrySellInstantly(s, "NOPE", out _), "없는 카드");
            Assert.AreEqual(0, s.Wallet.Money);
        }

        // 11-19 피드백 H: 레어 이하 모두 팔기 (견적은 아무것도 바꾸지 않고, 팔면 수퍼레어 이상 · 진열 카드는 그대로)
        [Test]
        public void SellAllUpToRare_SellsEveryCopy_KeepsHigherAndDisplayed()
        {
            s.Collection.Add("F-N1");
            s.Collection.Add("F-N1");
            s.Collection.Add("W-R1");
            s.Collection.Add("F-SR");
            s.SalesDisplay.Add("SN-L");
            SalesRules sales = f.Sales(new ScriptedRandom());
            const int expected = 1; // 노멀 0.2 × 2 + 레어 1 = 1.4 → 합계를 낸 뒤 내림

            Assert.AreEqual(3, sales.QuoteSellAllUpTo(s, CardRarity.Rare, out int quote));
            Assert.AreEqual(expected, quote);
            Assert.AreEqual(0, s.Wallet.Money, "견적은 팔지 않음");
            Assert.AreEqual(2, s.Collection.CountOf("F-N1"));

            Assert.AreEqual(3, sales.SellAllUpTo(s, CardRarity.Rare, out int total));
            Assert.AreEqual(expected, total);
            Assert.AreEqual(expected, s.Wallet.Money);
            Assert.AreEqual(0, s.Collection.CountOf("F-N1"));
            Assert.AreEqual(0, s.Collection.CountOf("W-R1"));
            Assert.AreEqual(1, s.Collection.CountOf("F-SR"), "수퍼레어는 남음");
            Assert.AreEqual(1, s.SalesDisplay.Count, "판매 등록 카드는 건드리지 않음");
            Assert.IsTrue(s.Collection.IsDiscovered("W-R1"), "팔아도 도감 유지");

            Assert.AreEqual(0, sales.SellAllUpTo(s, CardRarity.Rare, out total), "두 번째는 팔 것 없음");
            Assert.AreEqual(0, total);
        }

        // 노멀 묶음 판매: 13장 × 0.2 = 2.6 → 2
        [Test]
        public void SellAll_NormalsAreBulkSold_SumThenFloor()
        {
            for (int i = 0; i < 13; i++) s.Collection.Add("F-N1");
            SalesRules sales = f.Sales(new ScriptedRandom());
            Assert.AreEqual(13, sales.SellAllUpTo(s, CardRarity.Normal, out int total));
            Assert.AreEqual(2, total);
            Assert.AreEqual(2, s.Wallet.Money);
        }

        // 퇴근(Home)에서 집에서 등록한 카드가 DayEnd에서 지워지면 안 됨. 마지막 날에만 돌려놓음 (DayEndPhase가 이 함수를 씀)
        [Test]
        public void ReturnUnsoldOnFinalDay_KeepsDisplayBeforeFinalDay_ReturnsOnFinalDay()
        {
            s.Collection.Add("F-SR");
            SalesRules sales = f.Sales(new ScriptedRandom());
            sales.Display(s, "F-SR");

            Assert.AreEqual(0, sales.ReturnUnsoldOnFinalDay(s, 7), "1일차 끝: 진열 유지");
            CollectionAssert.AreEqual(new[] { "F-SR" }, s.SalesDisplay);

            for (int i = 0; i < 6; i++) s.AdvanceDay(); // 7일차
            Assert.AreEqual(1, sales.ReturnUnsoldOnFinalDay(s, 7), "마지막 날 끝: 보유로 복귀");
            Assert.AreEqual(0, s.SalesDisplay.Count);
            Assert.AreEqual(1, s.Collection.CountOf("F-SR"));
        }

        [Test]
        public void Customer_BuysWithinChance_RecordsNetAfterCommission()
        {
            s.Collection.Add("F-SR");
            // 둘러보기 순서 1번 → 구매 판정 0.18 < 0.35 × 0.4 × 1순위 1.3 = 0.182 → 구매
            SalesRules sales = f.Sales(new ScriptedRandom().Double(0.5, 0.18));
            sales.Display(s, "F-SR");

            CardData sold = Serve(sales, Fixture.Hot(CardElement.Fire)); // 1순위 불 ×1.5
            Assert.AreEqual("F-SR", sold.id);
            Assert.AreEqual(0, s.SalesDisplay.Count);
            Assert.AreEqual(1, s.Ledger.sales.Count);
            Assert.AreEqual(30, s.Ledger.sales[0].gross, "20 × 1.5");
            Assert.AreEqual(27, s.Ledger.sales[0].net, "기획서 5-4: 인기 속성 수퍼레어 27");
            Assert.AreEqual(1, s.Ledger.customersServed);
        }

        [Test]
        public void Customer_FailsChance_BuysShopStock()
        {
            s.Collection.Add("F-SR");
            // 0.15는 구매 확률 0.35 × 0.4 = 0.14 이상 → 탈락
            SalesRules sales = f.Sales(new ScriptedRandom().Double(0.5, 0.15));
            sales.Display(s, "F-SR");

            Assert.IsNull(Serve(sales, MarketDay.None));
            Assert.AreEqual(1, s.SalesDisplay.Count, "안 팔린 카드는 진열대에 남음");
            Assert.AreEqual(0, s.Ledger.sales.Count);
            Assert.AreEqual(1, s.Ledger.customersServed, "가게 재고 결제도 손님 1명");
        }

        [Test]
        public void Customer_WithEmptyDisplay_BuysShopStock()
        {
            Assert.IsNull(Serve(f.Sales(new ScriptedRandom()), MarketDay.None));
            Assert.AreEqual(1, s.Ledger.customersServed);
        }

        // 손님은 진열 카드를 모두 둘러봄 (DEC-041 Q8): 첫 카드에서 탈락해도 다음 카드를 살 수 있음 → 칸이 많을수록 잘 팔림
        [Test]
        public void Customer_BrowsesAllCards_BuysLaterCardWhenFirstFails()
        {
            s.Collection.Add("F-SR");
            s.Collection.Add("F-U");
            SalesRules sales = f.Sales(new ScriptedRandom().Double(0.99, 0.9, 0.5, 0.01)); // 순서 키: F-SR 0.99 > F-U 0.9 → F-SR 탈락(0.5) → F-U 구매(0.01)
            sales.Display(s, "F-SR");
            sales.Display(s, "F-U");

            CardData sold = Serve(sales, MarketDay.None);
            Assert.AreEqual("F-U", sold.id);
            CollectionAssert.AreEqual(new[] { "F-SR" }, s.SalesDisplay);
        }

        [Test]
        public void BuyChance_PopularRanksBoost_SnackNeverPopular()
        {
            SalesRules sales = f.Sales(new ScriptedRandom());
            var market = new MarketDay(CardElement.Fire, 2f, CardElement.Water, 1.5f, 2, "급등");
            Assert.AreEqual(0.35f * 0.4f * 1.3f, sales.BuyChance(f.Db.Get("F-SR"), market), 1e-5f, "1순위");
            Assert.AreEqual(0.35f * 0.4f, sales.BuyChance(f.Db.Get("F-SR"), MarketDay.None), 1e-5f, "인기 아님");
            Assert.AreEqual(0.03f * 0.4f, sales.BuyChance(f.Db.Get("SN-L"), market), 1e-5f, "간식은 인기 속성 없음");
        }

        [Test]
        public void ReturnUnsold_PutsCardsBack()
        {
            s.Collection.Add("F-SR");
            s.Collection.Add("F-U");
            SalesRules sales = f.Sales(new ScriptedRandom());
            sales.Display(s, "F-SR");
            sales.Display(s, "F-U");

            Assert.AreEqual(2, sales.ReturnUnsold(s));
            Assert.AreEqual(0, s.SalesDisplay.Count);
            Assert.AreEqual(1, s.Collection.CountOf("F-SR"));
            Assert.AreEqual(1, s.Collection.CountOf("F-U"));
        }

        [Test]
        public void Undisplay_ReturnsCard()
        {
            s.Collection.Add("F-U");
            SalesRules sales = f.Sales(new ScriptedRandom());
            sales.Display(s, "F-U");
            Assert.IsTrue(sales.TryUndisplay(s, "F-U"));
            Assert.AreEqual(1, s.Collection.CountOf("F-U"));
            Assert.IsFalse(sales.TryUndisplay(s, "F-U"), "진열대에 없으면 false");
        }
    }

    /// <summary>시세 일정 (경제시스템_설계 6 · 7장, DEC-041): 1순위 · 2순위, 강도 등급, 무한 모드 주기.</summary>
    public class MarketScheduleTests
    {
        EconomyConfig economy;

        [SetUp] public void SetUp() => economy = ScriptableObject.CreateInstance<EconomyConfig>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(economy);

        [Test]
        public void OfficialDays_TwoDayCycles_PrimaryChanges_SecondaryDiffers()
        {
            var schedule = new MarketSchedule(7, economy, new SystemRandomSource(1234));
            for (int day = 1; day <= 7; day++)
            {
                MarketDay m = schedule.For(day);
                Assert.AreNotEqual(CardElement.None, m.Primary, $"{day}일차");
                Assert.AreNotEqual(m.Primary, m.Secondary, $"{day}일차 1순위 ≠ 2순위");
                if (day % 2 == 0) Assert.AreEqual(schedule.For(day - 1).Primary, m.Primary, $"{day}일차는 전날과 같은 주기");
                else if (day > 1) Assert.AreNotEqual(schedule.For(day - 1).Primary, m.Primary, $"{day}일차에 새 주기 시작");
            }
            Assert.AreEqual(CardElement.None, schedule.For(0).Primary);
            Assert.AreEqual(schedule.For(7).Primary, schedule.For(8).Primary, "8일차는 7일차와 같은 주기 (2일 주기의 둘째 날)");
        }

        [Test]
        public void DebugNextTier_RaisesOneDayOnly_WrapsAfterLast()
        {
            var schedule = new MarketSchedule(7, economy, new SystemRandomSource(99));
            MarketDay before = schedule.For(1);
            MarketDay other = schedule.For(2); // 같은 주기의 다음 날
            MarketDay up = schedule.DebugNextTier(1);
            int expected = (before.TierIndex + 1) % economy.popularTiers.Length;
            Assert.AreEqual(expected, up.TierIndex);
            Assert.AreEqual(economy.popularTiers[expected].multiplier, up.PrimaryMultiplier, 1e-5f);
            Assert.AreEqual(1f + (up.PrimaryMultiplier - 1f) * economy.secondaryBonusShare, up.SecondaryMultiplier, 1e-5f);
            Assert.AreEqual(before.Primary, up.Primary, "속성은 그대로");
            Assert.AreEqual(up.TierIndex, schedule.For(1).TierIndex, "바뀐 값이 남음");
            Assert.AreEqual(other.TierIndex, schedule.For(2).TierIndex, "다른 날은 그대로");
            for (int i = 0; i < economy.popularTiers.Length - 1; i++) schedule.DebugNextTier(1);
            Assert.AreEqual(before.TierIndex, schedule.For(1).TierIndex, "한 바퀴 돌면 처음 단계");
        }

        [Test]
        public void TierPickedByWeight_SecondaryGetsHalfBonus()
        {
            // 1순위 불(0), 2순위 0 → 1순위와 같아 다시 뽑아 물(1), 강도 0.95 × 합 100 = 95 → 4번째 폭등 ×3
            var schedule = new MarketSchedule(2, economy, new ScriptedRandom().Int(0, 0, 1).Double(0.95));
            MarketDay m = schedule.For(1);
            Assert.AreEqual(CardElement.Fire, m.Primary);
            Assert.AreEqual(CardElement.Water, m.Secondary);
            Assert.AreEqual("폭등", m.TierName);
            Assert.AreEqual(3f, m.PrimaryMultiplier, 1e-5f);
            Assert.AreEqual(2f, m.SecondaryMultiplier, 1e-5f, "1 + (3 − 1) × 0.5");
            Assert.AreEqual(3f, m.MultiplierOf(CardElement.Fire), 1e-5f);
            Assert.AreEqual(1f, m.MultiplierOf(CardElement.Earth), 1e-5f);
            Assert.AreEqual(m.Primary, schedule.For(2).Primary, "2일 주기");
        }

        [Test]
        public void Endless_CycleLengthWithinRange_EveryDayHasMarket()
        {
            var schedule = new MarketSchedule(7, economy, new SystemRandomSource(99));
            int run = 1;
            for (int day = 9; day <= 60; day++)
            {
                MarketDay m = schedule.For(day);
                Assert.AreNotEqual(CardElement.None, m.Primary, $"{day}일차");
                if (m.Primary == schedule.For(day - 1).Primary) run++;
                else run = 1;
                Assert.LessOrEqual(run, 3, "연장 주기는 최대 3일 (이어지는 주기는 1순위가 늘 달라 구별됨)");
            }
        }

        // 무한 모드 연장 4일(11일차)부터 하락 속성 · 공식 기간과 연장 3일까지는 없음 (7-1)
        [Test]
        public void Endless_FallingElementFromExtraDay4_DiffersFromPopular()
        {
            var schedule = new MarketSchedule(7, economy, new SystemRandomSource(5));
            for (int day = 1; day <= 60; day++)
            {
                MarketDay m = schedule.For(day);
                if (m.Falling == CardElement.None) continue;
                Assert.AreNotEqual(m.Primary, m.Falling, $"{day}일차");
                Assert.AreNotEqual(m.Secondary, m.Falling, $"{day}일차");
                Assert.AreEqual(0.8f, m.MultiplierOf(m.Falling), 1e-5f);
                Assert.AreEqual(0, m.RankOf(m.Falling), "하락은 인기 아님");
            }
            for (int day = 1; day <= 7; day++) Assert.AreEqual(CardElement.None, schedule.For(day).Falling, $"공식 {day}일차");
            bool any = false;
            for (int day = 20; day <= 60; day++) any |= schedule.For(day).Falling != CardElement.None;
            Assert.IsTrue(any, "연장 4일 이후에는 하락 속성이 나옴");
        }

        [Test]
        public void FallingElement_LowersPrice_SlimeOnly()
        {
            var fixture = new Fixture();
            var rules = new EconomyRules(fixture.Economy, fixture.Rarity);
            var m = new MarketDay(CardElement.Water, 1.6f, CardElement.Earth, 1.3f, 1, "상승", CardElement.Fire, 0.8f);
            Assert.AreEqual(16, rules.GrossPrice(fixture.Db.Get("F-SR"), m), "불 수퍼레어 20 × 0.8");
            Assert.AreEqual(500, rules.GrossPrice(fixture.Db.Get("SN-L"), m), "간식은 그대로");
            fixture.Destroy();
        }

        [Test]
        public void SameSeed_SameSchedule()
        {
            var a = new MarketSchedule(7, economy, new SystemRandomSource(7));
            var b = new MarketSchedule(7, economy, new SystemRandomSource(7));
            for (int day = 1; day <= 20; day++)
            {
                Assert.AreEqual(a.For(day).Primary, b.For(day).Primary);
                Assert.AreEqual(a.For(day).Secondary, b.For(day).Secondary);
                Assert.AreEqual(a.For(day).TierIndex, b.For(day).TierIndex);
            }
        }
    }

    public class ScoreRulesTests
    {
        Fixture f;

        [SetUp] public void SetUp() => f = new Fixture();
        [TearDown] public void TearDown() => f.Destroy();

        [Test]
        public void Score_IsMoneyPlusCardValuePlusDex()
        {
            var s = new GameSession(120);
            s.Collection.Add("F-N1");
            s.Collection.Add("F-N1"); // 노말 매입가 0.2 × 2장
            s.Collection.Add("F-SR"); // 수퍼레어 5
            s.Collection.Add("SN-L"); // 레전더리 100
            s.Collection.Add("W-R1");
            s.Collection.Remove("W-R1"); // 팔았어도 도감은 유지

            FinalResult r = new ScoreRules(f.Score, f.Rarity, f.Db).Calculate(s);

            Assert.AreEqual(120, r.Money);
            Assert.AreEqual(105, r.CardValue, "매입가 합 105.4 → 내림 (DEC-041 Q7)");
            Assert.AreEqual(4, r.CollectedKinds);
            Assert.AreEqual(40, r.CollectionPoints);
            Assert.AreEqual(120 + 105 + 40, r.Score);
            Assert.AreEqual(5, r.TotalKinds);
            Assert.AreEqual(3, r.RareTotal, "수퍼레어 이상: F-SR, F-U, SN-L");
            Assert.AreEqual(2, r.RareCollected, "F-SR, SN-L");
        }

        // 랭크 기준 (DEC-041: 카드 가치가 매입가로 바뀌어 다시 정함, 시뮬레이션 추정): S 1800 / A 1300 / B 900 / C
        [TestCase(1800, "S", true)]
        [TestCase(1799, "A", true)]
        [TestCase(900, "B", true)]
        [TestCase(899, "C", false)]
        public void Rank_MatchesDesignThresholds(int money, string rank, bool success)
        {
            FinalResult r = new ScoreRules(f.Score, f.Rarity, f.Db).Calculate(new GameSession(money));
            Assert.AreEqual(rank, r.Rank.name);
            Assert.AreEqual(success, r.Rank.isSuccess);
        }
    }
}
