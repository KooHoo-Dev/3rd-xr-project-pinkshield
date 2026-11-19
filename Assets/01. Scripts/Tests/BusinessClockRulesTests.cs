using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    public class BusinessClockRulesTests
    {
        [Test]
        public void FastForwards_OnlyWhenIdleAfterSpawnWindow()
        {
            Assert.AreEqual(6f, BusinessClockRules.Speed(true, 0.8f, 0.7f, false, 0, 6f));
            Assert.AreEqual(1f, BusinessClockRules.Speed(true, 0.5f, 0.7f, false, 0, 6f), "손님이 아직 나올 수 있는 구간");
            Assert.AreEqual(1f, BusinessClockRules.Speed(true, 0.8f, 0.7f, true, 0, 6f), "손님이 남음");
            Assert.AreEqual(1f, BusinessClockRules.Speed(true, 0.8f, 0.7f, false, 1, 6f), "청소가 남음");
            Assert.AreEqual(1f, BusinessClockRules.Speed(false, 0.8f, 0.7f, false, 0, 6f), "빨리 감기가 꺼진 날");
        }

        [Test]
        public void ShopClock_MapsBusinessTo9To16_AndCleanupMinutes()
        {
            Assert.AreEqual("09:00", BusinessClockRules.Format(BusinessClockRules.HourAt(0f)));
            Assert.AreEqual("12:30", BusinessClockRules.Format(BusinessClockRules.HourAt(0.5f)));
            Assert.AreEqual("16:00", BusinessClockRules.Format(BusinessClockRules.HourAt(1f)));
            Assert.AreEqual("16:00", BusinessClockRules.Format(BusinessClockRules.HourAt(1.5f)), "넘쳐도 마감 시각");
            Assert.AreEqual("16:30", BusinessClockRules.Format(BusinessClockRules.HourAfterClose(30f)), "1초 = 1분");
        }
    }
}
