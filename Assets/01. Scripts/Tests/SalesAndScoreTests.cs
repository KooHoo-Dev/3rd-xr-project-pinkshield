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

        [Test]
        public void Display_TakesCardOutOfCollection_AndKeepsDexEntry()
        {
            s.Collection.Add("F-SR");
            Assert.IsTrue(f.Sales(new ScriptedRandom()).TryDisplay(s, "F-SR"));
            Assert.AreEqual(0, s.Collection.CountOf("F-SR"));
            Assert.IsTrue(s.Collection.IsDiscovered("F-SR"), "진열해도 도감은 유지");
            CollectionAssert.AreEqual(new[] { "F-SR" }, s.SalesDisplay);
        }

        [Test]
        public void Display_RejectsUnownedCard_AndFullDisplay()
        {
            SalesRules sales = f.Sales(new ScriptedRandom());
            Assert.IsFalse(sales.TryDisplay(s, "F-SR"), "보유하지 않은 카드");

            for (int i = 0; i < 5; i++) s.Collection.Add("F-SR");
            for (int i = 0; i < f.Economy.salesDisplaySlots; i++) Assert.IsTrue(sales.TryDisplay(s, "F-SR"));
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

        [TestCase("F-N1", 1)]   // 1 × 0.6 = 0.6 → 내림 0 → 최소 1
        [TestCase("W-R1", 3)]   // 5 × 0.6 = 3
        [TestCase("F-SR", 12)]  // 20 × 0.6
        [TestCase("F-U", 60)]
        [TestCase("SN-L", 300)]
        public void InstantPrice_IsBasePriceTimesRate_NoCommissionNoPopularBonus(string id, int expected)
        {
            Assert.AreEqual(expected, f.Sales(new ScriptedRandom()).InstantPrice(f.Db.Get(id)));
        }

        [Test]
        public void SellInstantly_PaysWallet_RemovesOneCard_KeepsDexEntry_NoLedger()
        {
            s.Collection.Add("F-N1");
            s.Collection.Add("F-N1");
            SalesRules sales = f.Sales(new ScriptedRandom());

            Assert.IsTrue(sales.TrySellInstantly(s, "F-N1", out int price), "노말도 즉시 판매 가능");
            Assert.AreEqual(1, price);
            Assert.AreEqual(1, s.Wallet.Money);
            Assert.AreEqual(1, s.Collection.CountOf("F-N1"), "1장만 빠짐");
            Assert.AreEqual(0, s.Ledger.sales.Count, "정산에 합산하지 않음");

            s.Collection.Add("SN-L");
            Assert.IsTrue(sales.TrySellInstantly(s, "SN-L", out price));
            Assert.AreEqual(300, price);
            Assert.AreEqual(301, s.Wallet.Money);
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

        // 퇴근(Home)에서 집에서 등록한 카드가 DayEnd에서 지워지면 안 됨. 마지막 날에만 돌려놓음 (DayEndPhase가 이 함수를 씀)
        [Test]
        public void ReturnUnsoldOnFinalDay_KeepsDisplayBeforeFinalDay_ReturnsOnFinalDay()
        {
            s.Collection.Add("F-SR");
            SalesRules sales = f.Sales(new ScriptedRandom());
            sales.TryDisplay(s, "F-SR");

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
            // 진열 0번 카드 선택, 0.34 < 수퍼레어 구매 확률 0.35 → 구매
            SalesRules sales = f.Sales(new ScriptedRandom().Int(0).Double(0.34));
            sales.TryDisplay(s, "F-SR");

            CardData sold = sales.ServeCustomer(s, CardElement.Fire); // 인기 속성 불
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
            // 0.35는 구매 확률 0.35 이상 → 탈락
            SalesRules sales = f.Sales(new ScriptedRandom().Int(0).Double(0.35));
            sales.TryDisplay(s, "F-SR");

            Assert.IsNull(sales.ServeCustomer(s, CardElement.None));
            Assert.AreEqual(1, s.SalesDisplay.Count, "안 팔린 카드는 진열대에 남음");
            Assert.AreEqual(0, s.Ledger.sales.Count);
            Assert.AreEqual(1, s.Ledger.customersServed, "가게 재고 결제도 손님 1명");
        }

        [Test]
        public void Customer_WithEmptyDisplay_BuysShopStock()
        {
            Assert.IsNull(f.Sales(new ScriptedRandom()).ServeCustomer(s, CardElement.None));
            Assert.AreEqual(1, s.Ledger.customersServed);
        }

        [Test]
        public void ReturnUnsold_PutsCardsBack()
        {
            s.Collection.Add("F-SR");
            s.Collection.Add("F-U");
            SalesRules sales = f.Sales(new ScriptedRandom());
            sales.TryDisplay(s, "F-SR");
            sales.TryDisplay(s, "F-U");

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
            sales.TryDisplay(s, "F-U");
            Assert.IsTrue(sales.TryUndisplay(s, "F-U"));
            Assert.AreEqual(1, s.Collection.CountOf("F-U"));
            Assert.IsFalse(sales.TryUndisplay(s, "F-U"), "진열대에 없으면 false");
        }
    }

    public class PopularElementScheduleTests
    {
        [Test]
        public void SameElementWithinCycle_ChangesBetweenCycles_SlimeOnly()
        {
            var schedule = new PopularElementSchedule(7, 2, new SystemRandomSource(1234));
            for (int day = 1; day <= 7; day++)
            {
                CardElement e = schedule.For(day);
                Assert.AreNotEqual(CardElement.None, e, $"{day}일차");
                if (day % 2 == 0) Assert.AreEqual(schedule.For(day - 1), e, $"{day}일차는 전날과 같은 주기");
                else if (day > 1) Assert.AreNotEqual(schedule.For(day - 1), e, $"{day}일차에 새 주기 시작");
            }
            Assert.AreEqual(CardElement.None, schedule.For(0));
            Assert.AreEqual(CardElement.None, schedule.For(8));
        }

        [Test]
        public void SameSeed_SameSchedule()
        {
            var a = new PopularElementSchedule(7, 2, new SystemRandomSource(7));
            var b = new PopularElementSchedule(7, 2, new SystemRandomSource(7));
            for (int day = 1; day <= 7; day++) Assert.AreEqual(a.For(day), b.For(day));
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
            s.Collection.Add("F-N1"); // 노말 1 × 2장
            s.Collection.Add("F-SR"); // 수퍼레어 20
            s.Collection.Add("SN-L"); // 레전더리 500
            s.Collection.Add("W-R1");
            s.Collection.Remove("W-R1"); // 팔았어도 도감은 유지

            FinalResult r = new ScoreRules(f.Score, f.Rarity, f.Db).Calculate(s);

            Assert.AreEqual(120, r.Money);
            Assert.AreEqual(2 + 20 + 500, r.CardValue);
            Assert.AreEqual(4, r.CollectedKinds);
            Assert.AreEqual(40, r.CollectionPoints);
            Assert.AreEqual(120 + 522 + 40, r.Score);
            Assert.AreEqual(5, r.TotalKinds);
            Assert.AreEqual(3, r.RareTotal, "수퍼레어 이상: F-SR, F-U, SN-L");
            Assert.AreEqual(2, r.RareCollected, "F-SR, SN-L");
        }

        // 기획서 5-8 랭크 기준: S 4000 / A 3400 / B 2400 / C
        [TestCase(4000, "S", true)]
        [TestCase(3999, "A", true)]
        [TestCase(2400, "B", true)]
        [TestCase(2399, "C", false)]
        public void Rank_MatchesDesignThresholds(int money, string rank, bool success)
        {
            FinalResult r = new ScoreRules(f.Score, f.Rarity, f.Db).Calculate(new GameSession(money));
            Assert.AreEqual(rank, r.Rank.name);
            Assert.AreEqual(success, r.Rank.isSuccess);
        }
    }
}
