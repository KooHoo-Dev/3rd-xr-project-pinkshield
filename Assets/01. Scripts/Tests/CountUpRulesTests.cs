using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>숫자 굴러 오르기 계산 (손목 SLIME PAY 카드)</summary>
    public class CountUpRulesTests
    {
        [Test]
        public void Value_StartsAtFrom_EndsAtTo()
        {
            Assert.AreEqual(100, CountUpRules.Value(100, 340, 0f));
            Assert.AreEqual(340, CountUpRules.Value(100, 340, 1f));
        }

        [Test]
        public void Value_OutOfRange_IsClamped()
        {
            Assert.AreEqual(100, CountUpRules.Value(100, 340, -1f));
            Assert.AreEqual(340, CountUpRules.Value(100, 340, 5f));
        }

        [Test]
        public void Value_Rising_NeverDecreases_AndStaysBetween()
        {
            int prev = 100;
            for (int i = 1; i <= 10; i++)
            {
                int v = CountUpRules.Value(100, 340, i / 10f);
                Assert.GreaterOrEqual(v, prev);
                Assert.LessOrEqual(v, 340);
                prev = v;
            }
        }

        [Test]
        public void Value_Falling_GoesDownToTarget()
        {
            int mid = CountUpRules.Value(500, 200, 0.5f);
            Assert.Less(mid, 500);
            Assert.Greater(mid, 200);
        }

        [Test]
        public void Progress_ClampsToZeroOne()
        {
            Assert.AreEqual(0f, CountUpRules.Progress(-0.2f));
            Assert.AreEqual(1f, CountUpRules.Progress(CountUpRules.Seconds * 3f));
            Assert.AreEqual(0.5f, CountUpRules.Progress(CountUpRules.Seconds * 0.5f), 0.0001f);
        }
    }
}
