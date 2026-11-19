using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>무한 모드 (11-20 J4): 마지막 날 판정 · 연장 글자 · 손님 추가 · 연장 합계</summary>
    public class DayRulesTests
    {
        [Test]
        public void LastDay_OnlyWithoutEndless()
        {
            Assert.IsTrue(DayRules.IsLastDay(7, 7, false));
            Assert.IsFalse(DayRules.IsLastDay(6, 7, false));
            Assert.IsFalse(DayRules.IsLastDay(7, 7, true), "무한 모드에는 마지막 날이 없음");
            Assert.IsFalse(DayRules.IsLastDay(12, 7, true));
        }

        [Test]
        public void DaySuffix_ShowsExtension()
        {
            Assert.AreEqual("/ 7일", DayRules.DaySuffix(3, 7));
            Assert.AreEqual("연장 2일", DayRules.DaySuffix(9, 7));
        }

        [Test]
        public void ExtraCustomers_EveryThreeDays_UpToMax()
        {
            Assert.AreEqual(0, DayRules.ExtraCustomers(7, 7, 3, 3));
            Assert.AreEqual(1, DayRules.ExtraCustomers(8, 7, 3, 3));
            Assert.AreEqual(1, DayRules.ExtraCustomers(10, 7, 3, 3));
            Assert.AreEqual(2, DayRules.ExtraCustomers(11, 7, 3, 3));
            Assert.AreEqual(3, DayRules.ExtraCustomers(30, 7, 3, 3), "최대");
        }

        [Test]
        public void EndlessState_CountsFromStart()
        {
            var e = new EndlessState();
            Assert.IsFalse(e.Active);
            e.Start(null, 7, 500, 20, 30);
            Assert.IsTrue(e.Active);
            Assert.AreEqual(2, e.ExtraDays(9));
            Assert.AreEqual(300, e.MoneyGained(800));
            Assert.AreEqual(5, e.PacksOpened(25));
            Assert.AreEqual(4, e.NewKinds(34));
            e.Finish();
            Assert.IsTrue(e.Finished);
        }
    }
}
