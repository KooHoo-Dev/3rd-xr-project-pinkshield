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
            Assert.AreEqual(EveningMood.WorkClean, EveningLines.WorkMoodFor(new DayLedger { customersServed = 3, cleanupsDone = 3 }));
            Assert.AreEqual(EveningMood.WorkPlain, EveningLines.WorkMoodFor(new DayLedger { customersServed = 3 }));
            Assert.AreEqual(EveningMood.WorkPlain, EveningLines.WorkMoodFor(null));
        }

        [Test]
        public void PackMood_HighestRarityFirst()
        {
            Assert.AreEqual(EveningMood.PackNone, EveningLines.PackMoodFor(new DayLedger()));
            Assert.AreEqual(EveningMood.PackLegendary, EveningLines.PackMoodFor(new DayLedger { packsOpened = 1, godPack = true }));
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
