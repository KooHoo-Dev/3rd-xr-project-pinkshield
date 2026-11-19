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
    }
}
