using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    public class EveningLinesTests
    {
        [Test]
        public void WorkMood_RegretsComeFirst()
        {
            Assert.AreEqual(EveningMood.WorkLost, EveningLines.WorkMoodFor(new DayLedger { customersLost = 1, customersServed = 9, cleanupsMissed = 2 }));
            Assert.AreEqual(EveningMood.WorkMessy, EveningLines.WorkMoodFor(new DayLedger { cleanupsMissed = 1, customersServed = 9 }));
            Assert.AreEqual(EveningMood.WorkBusy, EveningLines.WorkMoodFor(new DayLedger { customersServed = EveningLines.BusyCustomers, cleanupsDone = 3 }));
            Assert.AreEqual(EveningMood.WorkQuiet, EveningLines.WorkMoodFor(new DayLedger { customersServed = EveningLines.QuietCustomers, cleanupsDone = 3 }));
            Assert.AreEqual(EveningMood.WorkClean, EveningLines.WorkMoodFor(new DayLedger { customersServed = 6, cleanupsDone = 3 }));
            Assert.AreEqual(EveningMood.WorkPlain, EveningLines.WorkMoodFor(new DayLedger { customersServed = 6 }));
            Assert.AreEqual(EveningMood.WorkPlain, EveningLines.WorkMoodFor(null));
        }

        [Test]
        public void WorkMood_ThiefComesBeforeEverything()
        {
            Assert.AreEqual(EveningMood.WorkRobbed, EveningLines.WorkMoodFor(new DayLedger { freePackStolen = true, thievesCaught = 1, customersLost = 1 }));
            Assert.AreEqual(EveningMood.WorkThief, EveningLines.WorkMoodFor(new DayLedger { thievesCaught = 1, customersLost = 1 }));
        }

        [Test]
        public void MoneyMood_FromSalesAndDisplay()
        {
            var none = new DayLedger();
            Assert.IsNull(EveningLines.MoneyMoodFor(none, 0), "판매도 진열도 없으면 돈 줄 없음");
            Assert.AreEqual(EveningMood.MoneyUnsold, EveningLines.MoneyMoodFor(none, 2));

            var small = new DayLedger();
            small.sales.Add(new DayLedger.Sale { cardId = "a", gross = 20, net = 18 });
            Assert.AreEqual(EveningMood.MoneySold, EveningLines.MoneyMoodFor(small, 1));

            var big = new DayLedger();
            big.sales.Add(new DayLedger.Sale { cardId = "a", gross = 60, net = 55 });
            big.sales.Add(new DayLedger.Sale { cardId = "b", gross = 50, net = EveningLines.BigSaleNet - 55 });
            Assert.AreEqual(EveningMood.MoneyBig, EveningLines.MoneyMoodFor(big, 0), "여러 장 합이 기준 이상");
        }

        [Test]
        public void For_AddsMoneyLine_InTheMiddle()
        {
            Assert.AreEqual(2, EveningLines.For(2, new DayLedger()).Split('\n').Length);
            string s = EveningLines.For(2, new DayLedger(), EveningLines.Parse("MoneyUnsold|그대로네"), onDisplay: 1);
            string[] lines = s.Split('\n');
            Assert.AreEqual(3, lines.Length);
            Assert.AreEqual("그대로네", lines[1], "일 → 돈 → 카드 순서");
        }

        [Test]
        public void Defaults_NeverSayCustomerCount()
        {
            // 11-20 피드백: 손님 수는 숫자 대신 바쁨 · 한가로 표현. 모든 상황 · 여러 날을 돌려 봄
            var ledgers = new[]
            {
                new DayLedger { customersServed = 7 },                                   // Plain
                new DayLedger { customersServed = 7, cleanupsDone = 2 },                 // Clean
                new DayLedger { customersServed = 3 },                                   // Quiet
                new DayLedger { customersServed = 9 },                                   // Busy
                new DayLedger { customersServed = 7, customersLost = 1 },                // Lost
                new DayLedger { customersServed = 7, cleanupsMissed = 1 },               // Messy
                new DayLedger { customersServed = 7, thievesCaught = 1 },                // Thief
                new DayLedger { customersServed = 7, freePackStolen = true },            // Robbed
            };
            foreach (var ledger in ledgers)
                for (int day = 1; day <= 12; day++)
                    StringAssert.DoesNotContain($"{ledger.customersServed}명", EveningLines.For(day, ledger));
        }

        [Test]
        public void PackMood_HighestRarityFirst()
        {
            Assert.AreEqual(EveningMood.PackNone, EveningLines.PackMoodFor(new DayLedger()));
            Assert.AreEqual(EveningMood.PackGodPack, EveningLines.PackMoodFor(new DayLedger { packsOpened = 1, godPack = true, bestRarity = (int)CardRarity.Legendary }), "갓팩은 레전더리보다 우선");
            Assert.AreEqual(EveningMood.PackLegendary, EveningLines.PackMoodFor(new DayLedger { packsOpened = 1, legendaryPack = true }));
            Assert.AreEqual(EveningMood.PackUnique, EveningLines.PackMoodFor(new DayLedger { packsOpened = 2, bestRarity = (int)CardRarity.Unique, newCards = 9 }));
            Assert.AreEqual(EveningMood.PackSuperRare, EveningLines.PackMoodFor(new DayLedger { packsOpened = 2, bestRarity = (int)CardRarity.SuperRare }));
            Assert.AreEqual(EveningMood.PackNew, EveningLines.PackMoodFor(new DayLedger { packsOpened = 3, bestRarity = (int)CardRarity.Rare, newCards = EveningLines.ManyNewCards }));
            Assert.AreEqual(EveningMood.PackPlain, EveningLines.PackMoodFor(new DayLedger { packsOpened = 3, bestRarity = (int)CardRarity.Rare, newCards = 1 }));
        }

        [Test]
        public void For_IsTwoLines_WithNumbersFilledIn()
        {
            var custom = EveningLines.Parse("WorkBusy|손님 {customers}명\nPackNew|새 카드 {new}장\n# 주석\n잘못된줄\nNope|무시");
            string s = EveningLines.For(2, new DayLedger { customersServed = 6, packsOpened = 2, bestRarity = 0, newCards = 7 }, custom);
            Assert.AreEqual("손님 6명\n새 카드 7장", s);
        }

        [Test]
        public void For_FallsBackToDefaults_AndVariesByDay()
        {
            var ledger = new DayLedger { customersServed = 3 };
            string day2 = EveningLines.For(2, ledger);
            string day3 = EveningLines.For(3, ledger);
            StringAssert.Contains("\n", day2);
            Assert.AreNotEqual(day2, day3, "연속 날짜에는 다른 문장");
            StringAssert.DoesNotContain("{", day2, "자리표시가 남으면 안 됨");
        }
    }
}
