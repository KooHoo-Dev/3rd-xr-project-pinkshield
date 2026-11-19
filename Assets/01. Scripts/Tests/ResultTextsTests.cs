using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>정산 · 최종 판 글자 (타이틀연출_손님_설계 0-3)</summary>
    public class ResultTextsTests
    {
        EconomyConfig economy;
        RarityTable rarity;

        [SetUp]
        public void SetUp()
        {
            economy = ScriptableObject.CreateInstance<EconomyConfig>();
            rarity = ScriptableObject.CreateInstance<RarityTable>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(economy);
            Object.DestroyImmediate(rarity);
        }

        [Test]
        public void Signed_ShowsPlusMinusAndZero()
        {
            Assert.AreEqual("+5", ResultTexts.Signed(5));
            Assert.AreEqual("−3", ResultTexts.Signed(-3));
            Assert.AreEqual("0", ResultTexts.Signed(0));
        }

        [Test]
        public void MoneyText_DollarsWithSignAndThousands()
        {
            Assert.AreEqual("$1,250", MoneyText.Format(1250));
            Assert.AreEqual("+$40", MoneyText.Signed(40));
            Assert.AreEqual("−$3", MoneyText.Signed(-3));
            Assert.AreEqual("$0", MoneyText.Signed(0));
        }

        [Test]
        public void Settlement_ListsEveryLineAndTotal()
        {
            var rules = new EconomyRules(economy, rarity);
            var l = new DayLedger { basePay = 30, cleanupsDone = 2, cleanupsMissed = 1, checkoutsFast = 3, customersLost = 1, thievesCaught = 1 };
            l.sales.Add(new DayLedger.Sale { cardId = "x", gross = 50, net = 45 });
            string s = ResultTexts.Settlement(l, rules, 123, 456);

            StringAssert.Contains("기본급", s);
            StringAssert.Contains("+$30", s);
            StringAssert.Contains("못 치움 1곳", s);
            StringAssert.Contains(MoneyText.Signed(rules.CleanupTotal(l)), s);
            StringAssert.Contains("떠난 손님 1명", s);
            StringAssert.Contains("판매 1장", s);
            StringAssert.Contains("+$45", s);
            StringAssert.Contains("도둑 1명", s);
            StringAssert.Contains("+$123", s);
            StringAssert.Contains("$456", s);
        }

        [Test]
        public void Settlement_QuietDay_HidesOptionalLines()
        {
            string s = ResultTexts.Settlement(new DayLedger { basePay = 30 }, new EconomyRules(economy, rarity), 30, 30);
            StringAssert.DoesNotContain("못 치움", s);
            StringAssert.DoesNotContain("떠난 손님", s);
            StringAssert.DoesNotContain("도둑", s);
        }

        [Test]
        public void Final_WorksWithoutRankOrBestCard()
        {
            string s = ResultTexts.Final(new FinalResult { Score = 999, Money = 500, CollectedKinds = 3, TotalKinds = 50 }, "계약 연장");
            StringAssert.Contains("999", s);
            StringAssert.Contains("3/50", s);
            StringAssert.Contains("계약 연장", s);
            StringAssert.DoesNotContain("최고의 카드", s);
            Assert.AreEqual("", ResultTexts.Final(null, "x"));
        }
    }
}
