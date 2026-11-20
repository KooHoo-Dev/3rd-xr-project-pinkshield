using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>기획서 수치를 기대값으로 쓰는 테스트. 수치를 바꾸면 여기서 바로 드러납니다.</summary>
    public class PackOddsTests
    {
        PackOddsConfig odds;

        [SetUp] public void SetUp() => odds = ScriptableObject.CreateInstance<PackOddsConfig>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(odds);

        // 기획서 5-3 ② 단서 보정표 (소수 첫째 자리 반올림 값)
        [TestCase(PackSlotType.Rare, 1, 28.0f, 54.0f, 16.0f, 1.8f, 0.2f)]
        [TestCase(PackSlotType.Hit, 1, 0f, 49.0f, 37.5f, 12.6f, 0.9f)]
        [TestCase(PackSlotType.Rare, 2, 25.9f, 50.1f, 21.4f, 2.4f, 0.2f)]
        [TestCase(PackSlotType.Hit, 2, 0f, 32.0f, 50.0f, 16.8f, 1.2f)]
        public void ClueWeights_MatchDesignTable(PackSlotType slot, int clues, float n, float r, float sr, float u, float l)
        {
            RarityWeights w = odds.GetSlotWeights(slot, clues);
            const float tol = 0.35f; // 기획서 표는 반올림 값
            Assert.AreEqual(n, w.normal, tol, "노말");
            Assert.AreEqual(r, w.rare, tol, "레어");
            Assert.AreEqual(sr, w.superRare, tol, "수퍼레어");
            Assert.AreEqual(u, w.unique, tol, "유니크");
            Assert.AreEqual(l, w.legendary, tol, "레전더리");
            Assert.AreEqual(100f, w.Sum, 0.01f, "합계");
        }

        [Test]
        public void CommonSlot_IsNotBoostedByClues()
        {
            Assert.AreEqual(odds.commonSlot.normal, odds.GetSlotWeights(PackSlotType.Common, 2).normal);
        }

        [Test]
        public void BaseSlots_SumTo100()
        {
            Assert.AreEqual(100f, odds.commonSlot.Sum, 0.01f);
            Assert.AreEqual(100f, odds.rareSlot.Sum, 0.01f);
            Assert.AreEqual(100f, odds.hitSlot.Sum, 0.01f);
        }
    }

    public class EconomyRulesTests
    {
        EconomyConfig economy;
        RarityTable rarity;
        EconomyRules rules;

        [SetUp]
        public void SetUp()
        {
            economy = ScriptableObject.CreateInstance<EconomyConfig>();
            rarity = ScriptableObject.CreateInstance<RarityTable>();
            rules = new EconomyRules(economy, rarity);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(economy);
            Object.DestroyImmediate(rarity);
        }

        static CardData Card(CardKind kind, CardElement element, CardRarity r)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.kind = kind; c.element = element; c.rarity = r;
            return c;
        }

        // 기획서 5-4 가격표: 판매 수령액 / 인기 속성일 때 수령액
        [TestCase(CardRarity.Normal, 1, 1)]
        [TestCase(CardRarity.Rare, 4, 6)]
        [TestCase(CardRarity.SuperRare, 18, 27)]
        [TestCase(CardRarity.Unique, 90, 135)]
        [TestCase(CardRarity.Legendary, 450, 675)]
        public void SellPrice_MatchesDesignTable(CardRarity r, int normalPrice, int popularPrice)
        {
            CardData card = Card(CardKind.Slime, CardElement.Fire, r);
            Assert.AreEqual(normalPrice, rules.SellPrice(card, Fixture.Hot(CardElement.Water)));
            Assert.AreEqual(popularPrice, rules.SellPrice(card, Fixture.Hot(CardElement.Fire)));
            Object.DestroyImmediate(card);
        }

        [Test]
        public void GrossPrice_NoFloatRoundingLossWithOddMultiplier()
        {
            // 20 × 1.3f = 25.9999… → 내림하면 25가 되던 버그
            CardData card = Card(CardKind.Slime, CardElement.Fire, CardRarity.SuperRare);
            Assert.AreEqual(26, rules.GrossPrice(card, Fixture.Hot(CardElement.Fire, 1.3f)));
            Object.DestroyImmediate(card);
        }

        [Test]
        public void PopularElement_DoesNotApplyToFieldOrSnack()
        {
            CardData snack = Card(CardKind.Snack, CardElement.None, CardRarity.Unique);
            Assert.AreEqual(90, rules.SellPrice(snack, MarketDay.None));
            Assert.IsFalse(rules.IsPopular(snack, Fixture.Hot(CardElement.None)));
            Object.DestroyImmediate(snack);
        }

        [Test]
        public void Settlement_BasePayPlusCleanupPlusSales()
        {
            var ledger = new DayLedger { basePay = 40, cleanupsDone = 4, cleanupsMissed = 1 };
            ledger.sales.Add(new DayLedger.Sale { cardId = "F-SR", gross = 20, net = 18 });
            Assert.AreEqual(40 + 20 - 3 + 18, rules.SettlementTotal(ledger));
        }

        [Test]
        public void Settlement_AddsCheckoutBonusMinusLostCustomers()
        {
            economy.checkoutBonus = 2;
            economy.checkoutPenalty = 1;
            var ledger = new DayLedger { basePay = 40, checkoutsFast = 5, customersLost = 3 };
            Assert.AreEqual(5 * 2 - 3 * 1, rules.CheckoutTotal(ledger));
            Assert.AreEqual(40 + 7, rules.SettlementTotal(ledger));
        }

        [Test]
        public void Settlement_LostCustomersHaveNoPenaltyByDefault()
        {
            var ledger = new DayLedger { basePay = 40, customersLost = 4 };
            Assert.AreEqual(0, rules.CheckoutTotal(ledger));
            Assert.AreEqual(40, rules.SettlementTotal(ledger));
        }

        [Test]
        public void FastCheckout_LimitGrowsFromSecondItem()
        {
            economy.checkoutFastSeconds = 20f;
            economy.checkoutFastPerExtraItemSeconds = 8f;
            Assert.IsTrue(rules.IsFastCheckout(20f, 1));
            Assert.IsFalse(rules.IsFastCheckout(20.5f, 1));
            Assert.IsTrue(rules.IsFastCheckout(36f, 3));   // 20 + 8 × 2
            Assert.IsFalse(rules.IsFastCheckout(36.5f, 3));
        }
    }

    public class SessionTests
    {
        [Test]
        public void Wallet_CannotOverspend()
        {
            var w = new Wallet(10);
            Assert.IsFalse(w.TrySpend(11));
            Assert.IsTrue(w.TrySpend(10));
            Assert.AreEqual(0, w.Money);
        }

        [Test]
        public void Collection_KeepsDiscoveredAfterSelling()
        {
            var c = new Collection();
            Assert.IsTrue(c.Add("F-N1"));
            Assert.IsFalse(c.Add("F-N1"));
            Assert.AreEqual(2, c.CountOf("F-N1"));
            c.Remove("F-N1");
            c.Remove("F-N1");
            Assert.AreEqual(0, c.CountOf("F-N1"));
            Assert.IsTrue(c.IsDiscovered("F-N1"), "도감은 팔아도 유지 (설계서 Q9)");
            Assert.AreEqual(1, c.DiscoveredCount);
        }

        [Test]
        public void AdvanceDay_ResetsLedger()
        {
            var s = new GameSession(0);
            s.Ledger.cleanupsDone = 3;
            s.AdvanceDay();
            Assert.AreEqual(2, s.Day);
            Assert.AreEqual(0, s.Ledger.cleanupsDone);
        }
    }
}
