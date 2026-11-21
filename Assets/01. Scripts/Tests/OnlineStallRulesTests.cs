using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>슬라임마켓 (DEC-040 U2) · "가구 덕분에" 정산 줄 (UpgradeCredits).</summary>
    public class OnlineStallRulesTests
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

        OnlineStallRules Rules(IRandomSource rng) =>
            new OnlineStallRules(f.Economy, f.Rarity, new EconomyRules(f.Economy, f.Rarity), f.Db, rng);

        [Test]
        public void Put_TakesCardOutOfCollection_TakeReturnsIt()
        {
            s.Collection.Add("F-SR");
            OnlineStallRules stall = Rules(new ScriptedRandom());
            Assert.IsTrue(stall.Put(s, "F-SR"));
            Assert.AreEqual("F-SR", s.OnlineStall);
            Assert.AreEqual(0, s.Collection.CountOf("F-SR"));
            Assert.IsTrue(stall.Take(s));
            Assert.IsNull(s.OnlineStall);
            Assert.AreEqual(1, s.Collection.CountOf("F-SR"));
        }

        [Test]
        public void Put_RejectsLowRarityAndUnowned_SwapsExisting()
        {
            s.Collection.Add("W-R1");
            OnlineStallRules stall = Rules(new ScriptedRandom());
            Assert.IsFalse(stall.Put(s, "W-R1"), "레어는 못 올림");
            Assert.IsFalse(stall.Put(s, "F-U"), "없는 카드");

            s.Collection.Add("F-SR");
            s.Collection.Add("F-U");
            stall.Put(s, "F-SR");
            Assert.IsTrue(stall.Put(s, "F-U"), "다른 카드를 올리면 바꿈");
            Assert.AreEqual("F-U", s.OnlineStall);
            Assert.AreEqual(1, s.Collection.CountOf("F-SR"), "먼저 올린 카드는 돌아옴");
        }

        [Test]
        public void ResolveNight_SellsWithinChance_PaysAfterCommission()
        {
            s.Collection.Add("F-SR");
            // 수퍼레어 구매 확률 0.35 × 0.5 = 0.175 → 0.17이면 팔림
            OnlineStallRules stall = Rules(new ScriptedRandom().Double(0.17));
            stall.Put(s, "F-SR");
            Assert.IsTrue(stall.ResolveNight(s, Fixture.Hot(CardElement.Fire, 2f), out CardData sold, out int net));
            Assert.AreEqual("F-SR", sold.id);
            Assert.AreEqual(32, net, "20 × 2.0 = 40, 수수료 20% → 32");
            Assert.AreEqual(32, s.Wallet.Money);
            Assert.IsNull(s.OnlineStall);
        }

        [Test]
        public void ResolveNight_NotSold_KeepsCard()
        {
            s.Collection.Add("F-SR");
            OnlineStallRules stall = Rules(new ScriptedRandom().Double(0.18));
            stall.Put(s, "F-SR");
            Assert.IsFalse(stall.ResolveNight(s, MarketDay.None, out _, out _));
            Assert.AreEqual("F-SR", s.OnlineStall);
            Assert.AreEqual(0, s.Wallet.Money);
        }

        [Test]
        public void StallCard_CountsInFinalScore()
        {
            s.Collection.Add("F-U");
            int before = new ScoreRules(f.Score, f.Rarity, f.Db).Calculate(s).CardValue;
            Rules(new ScriptedRandom()).Put(s, "F-U");
            Assert.AreEqual(before, new ScoreRules(f.Score, f.Rarity, f.Db).Calculate(s).CardValue);
        }

        [Test]
        public void ExtraSlotSale_IsCreditedToFurniture()
        {
            SalesRules sales = f.Sales(new ScriptedRandom());
            for (int i = 0; i < 5; i++) s.SalesDisplay.Add("F-SR"); // 기본 4칸 + 늘린 칸 1
            CardData card = f.Db.Get("F-SR");
            s.SalesDisplay[4] = "F-U";
            Assert.IsTrue(sales.CompleteSale(s, f.Db.Get("F-U"), MarketDay.None, out int net));
            Assert.AreEqual(1, s.Ledger.extraSlotSales, "다섯 번째 칸 = 바인더로 늘린 칸");
            Assert.AreEqual(net, s.Ledger.extraSlotNet);
            Assert.IsTrue(sales.CompleteSale(s, card, MarketDay.None, out _));
            Assert.AreEqual(1, s.Ledger.extraSlotSales, "기본 칸 판매는 안 셈");
        }

        [Test]
        public void CreditsLine_ListsWhatFurnitureDid_NullWhenNothing()
        {
            var l = new DayLedger();
            Assert.IsNull(UpgradeCredits.Line(l));
            l.extraSlotSales = 1; l.extraSlotNet = 27;
            l.patienceSaved = 2;
            l.onlineSaleCard = "촛불 슬라임"; l.onlineSaleNet = 32;
            string line = UpgradeCredits.Line(l);
            StringAssert.StartsWith("가구 덕분에:", line);
            StringAssert.Contains("늘린 판매 칸에서 1장", line);
            StringAssert.Contains("기다려 준 손님 2명", line);
            StringAssert.Contains("슬라임마켓 「촛불 슬라임」", line);
        }

        [Test]
        public void SavedByBed_OnlyWhenWaitedPastBasePatience()
        {
            Assert.IsTrue(UpgradeCredits.SavedByBed(32f, 30f, 1.15f));
            Assert.IsFalse(UpgradeCredits.SavedByBed(28f, 30f, 1.15f), "기본 시간 안");
            Assert.IsFalse(UpgradeCredits.SavedByBed(32f, 30f, 1f), "침대 업그레이드 없음");
        }
    }
}
