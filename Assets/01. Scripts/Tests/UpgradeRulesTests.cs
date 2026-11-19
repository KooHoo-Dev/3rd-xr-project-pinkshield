using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>방 업그레이드 규칙 · 택배 흐름 · 효과 (집_업그레이드_설계 3장 E2, DEC-031).</summary>
    public class UpgradeRulesTests
    {
        Fixture f;
        UpgradeCatalog catalog;
        UpgradeRules rules;
        GameSession s;

        [SetUp]
        public void SetUp()
        {
            f = new Fixture();
            catalog = ScriptableObject.CreateInstance<UpgradeCatalog>(); // 코드 기본값 = 설계서 표
            rules = new UpgradeRules(catalog);
            s = new GameSession(1000);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(catalog);
            f.Destroy();
        }

        [Test]
        public void DefaultCatalog_MatchesDesignTable()
        {
            Assert.AreEqual(40, catalog.Get(UpgradeKind.Showcase).levels[0].price);
            Assert.AreEqual(100, catalog.Get(UpgradeKind.Showcase).levels[1].price);
            Assert.AreEqual(60, catalog.Get(UpgradeKind.Binder).levels[0].price);
            Assert.AreEqual(140, catalog.Get(UpgradeKind.Binder).levels[1].price);
            Assert.AreEqual(3, rules.MaxLevel(UpgradeKind.Bed));
            Assert.AreEqual(2, rules.MaxLevel(UpgradeKind.Poster));
            Assert.AreEqual(10, rules.MaxShowcaseSlots());
        }

        [Test]
        public void Buy_SpendsMoney_ArrivesNextDay_OpenRaisesLevel()
        {
            Assert.AreEqual(BuyResult.Ok, rules.TryBuy(s, UpgradeKind.Showcase, out int price));
            Assert.AreEqual(40, price);
            Assert.AreEqual(960, s.Wallet.Money);
            Assert.AreEqual(UpgradeStatus.Ordered, s.Upgrades.StatusOf(UpgradeKind.Showcase));
            Assert.AreEqual(3, rules.ShowcaseSlots(s.Upgrades), "주문만으로는 효과 없음");

            s.AdvanceDay();
            Assert.AreEqual(UpgradeStatus.Arrived, s.Upgrades.StatusOf(UpgradeKind.Showcase));
            CollectionAssert.AreEqual(new[] { UpgradeKind.Showcase }, s.Upgrades.Arrived());
            Assert.AreEqual(3, rules.ShowcaseSlots(s.Upgrades), "상자를 열기 전");

            Assert.IsTrue(s.Upgrades.Open(UpgradeKind.Showcase));
            Assert.AreEqual(2, s.Upgrades.LevelOf(UpgradeKind.Showcase));
            Assert.AreEqual(6, rules.ShowcaseSlots(s.Upgrades));
            Assert.IsFalse(s.Upgrades.Open(UpgradeKind.Showcase));
        }

        [Test]
        public void UnopenedBox_OpensItselfNextMorning()
        {
            rules.TryBuy(s, UpgradeKind.Bed, out _);
            s.AdvanceDay(); // 도착
            s.AdvanceDay(); // 안 열고 하루 지남 → 저절로 열림
            Assert.AreEqual(2, s.Upgrades.LevelOf(UpgradeKind.Bed));
            Assert.AreEqual(UpgradeStatus.None, s.Upgrades.StatusOf(UpgradeKind.Bed));
        }

        [Test]
        public void CannotBuySameKindWhilePending_ButOtherKindIsFine()
        {
            rules.TryBuy(s, UpgradeKind.Binder, out _);
            Assert.AreEqual(BuyResult.Pending, rules.Check(s, UpgradeKind.Binder));
            Assert.AreEqual(BuyResult.Ok, rules.Check(s, UpgradeKind.Bed));
            s.AdvanceDay();
            Assert.AreEqual(BuyResult.Pending, rules.Check(s, UpgradeKind.Binder), "상자가 와 있어도 아직");
            s.Upgrades.Open(UpgradeKind.Binder);
            Assert.AreEqual(BuyResult.Ok, rules.Check(s, UpgradeKind.Binder));
            Assert.AreEqual(140, rules.NextLevel(s.Upgrades, UpgradeKind.Binder).price);
        }

        [Test]
        public void NotEnoughMoney_NothingChanges()
        {
            var poor = new GameSession(39);
            Assert.AreEqual(BuyResult.NotEnoughMoney, rules.TryBuy(poor, UpgradeKind.Showcase, out int price));
            Assert.AreEqual(0, price);
            Assert.AreEqual(39, poor.Wallet.Money);
            Assert.AreEqual(0, poor.Upgrades.Spent);
        }

        [Test]
        public void MaxLevel_CannotBuyMore()
        {
            BuyAndOpen(UpgradeKind.Poster);
            Assert.AreEqual(BuyResult.MaxLevel, rules.Check(s, UpgradeKind.Poster));
            Assert.IsNull(rules.NextLevel(s.Upgrades, UpgradeKind.Poster));
        }

        [Test]
        public void Effects_ByLevel()
        {
            Assert.AreEqual(1f, rules.PatienceMultiplier(s.Upgrades), 1e-5f);
            Assert.AreEqual(1, rules.ForecastDays(s.Upgrades));
            Assert.IsFalse(rules.HasPriceList(s.Upgrades));
            Assert.IsFalse(rules.ShowcaseLight(s.Upgrades));

            BuyAndOpen(UpgradeKind.Bed);
            BuyAndOpen(UpgradeKind.Computer);
            Assert.AreEqual(1.1f, rules.PatienceMultiplier(s.Upgrades), 1e-5f);
            Assert.AreEqual(0, rules.ExtraFreePacks(s.Upgrades));
            Assert.AreEqual(2, rules.ForecastDays(s.Upgrades));

            BuyAndOpen(UpgradeKind.Bed);
            BuyAndOpen(UpgradeKind.Computer);
            BuyAndOpen(UpgradeKind.Showcase);
            BuyAndOpen(UpgradeKind.Showcase);
            Assert.AreEqual(1.2f, rules.PatienceMultiplier(s.Upgrades), 1e-5f);
            Assert.AreEqual(1, rules.ExtraFreePacks(s.Upgrades));
            Assert.IsTrue(rules.HasPriceList(s.Upgrades));
            Assert.AreEqual(10, rules.ShowcaseSlots(s.Upgrades));
            Assert.IsTrue(rules.ShowcaseLight(s.Upgrades));
        }

        [Test]
        public void BinderUpgrade_AddsSalesSlotsAndInstantRate()
        {
            var sales = new SalesRules(f.Economy, f.Rarity, new EconomyRules(f.Economy, f.Rarity), f.Db, new ScriptedRandom(), rules);
            CardData unique = f.Db.Get("F-U"); // 기준가 100
            Assert.AreEqual(4, sales.SlotCount(s));
            Assert.AreEqual(60, sales.InstantPrice(unique, s));

            BuyAndOpen(UpgradeKind.Binder);
            Assert.AreEqual(6, sales.SlotCount(s));
            Assert.AreEqual(60, sales.InstantPrice(unique, s));

            BuyAndOpen(UpgradeKind.Binder);
            Assert.IsTrue(rules.GoldBinder(s.Upgrades));
            Assert.AreEqual(70, sales.InstantPrice(unique, s));
            s.Collection.Add("F-U");
            Assert.IsTrue(sales.TrySellInstantly(s, "F-U", out int got));
            Assert.AreEqual(70, got);
        }

        [Test]
        public void Score_AddsFurnitureValueAtFullPrice()
        {
            var score = new ScoreRules(f.Score, f.Rarity, f.Db);
            int before = score.Calculate(s).Score;
            rules.TryBuy(s, UpgradeKind.Bed, out int price); // 돈 70 → 가구 70
            FinalResult after = score.Calculate(s);
            Assert.AreEqual(70, price);
            Assert.AreEqual(70, after.FurnitureValue);
            Assert.AreEqual(before, after.Score, "산 가격 100%를 자산으로: 점수는 그대로 (DEC-031 Q2)");
        }

        [Test]
        public void FinalText_ShowsFurnitureAndShowcaseLinesOnlyWhenPresent()
        {
            var score = new ScoreRules(f.Score, f.Rarity, f.Db);
            string plain = ResultTexts.Final(score.Calculate(s), "");
            StringAssert.DoesNotContain("가구 값", plain);
            StringAssert.DoesNotContain("진열장", plain);

            rules.TryBuy(s, UpgradeKind.Rug, out _);
            s.Collection.Add("F-SR");
            ShowcaseRules.TryAdd(s, "F-SR", 3);
            string text = ResultTexts.Final(score.Calculate(s), "");
            StringAssert.Contains("가구 값", text);
            StringAssert.Contains("$20", text);
            StringAssert.Contains("진열장 1장", text);
        }

        void BuyAndOpen(UpgradeKind kind)
        {
            Assert.AreEqual(BuyResult.Ok, rules.TryBuy(s, kind, out _), kind.ToString());
            s.AdvanceDay();
            Assert.IsTrue(s.Upgrades.Open(kind));
        }
    }
}
