using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>11-20 J1: 모니터 쪽 넘기기 — 왼쪽으로 끌면 다음 쪽, 짧게 끌면 제자리, 끝에서 멈춤</summary>
    public class PagerRulesTests
    {
        [Test]
        public void DragLeft_GoesNext_DragRight_GoesBack()
        {
            Assert.AreEqual(1, PagerRules.AfterDrag(0, 2, -300f, 1000f));
            Assert.AreEqual(0, PagerRules.AfterDrag(1, 2, 300f, 1000f));
        }

        [Test]
        public void ShortDrag_Stays()
        {
            Assert.AreEqual(0, PagerRules.AfterDrag(0, 2, -100f, 1000f));
        }

        [Test]
        public void StopsAtEnds()
        {
            Assert.AreEqual(1, PagerRules.AfterDrag(1, 2, -500f, 1000f));
            Assert.AreEqual(0, PagerRules.AfterDrag(0, 2, 500f, 1000f));
        }
    }
}
