using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>바닥 얼룩 문지르기 (FIX F5).</summary>
    public class StainRulesTests
    {
        static readonly Vector3 Center = new Vector3(1f, 0f, 2f);

        [Test]
        public void Scrubbed_CountsOnlyOnStain_NearFloor_AndMoving()
        {
            Vector3 a = Center + new Vector3(0f, 0.05f, 0f), b = a + new Vector3(0.1f, 0f, 0f);
            Assert.AreEqual(0.1f, StainRules.Scrubbed(a, b, 0.1f, Center, 0.3f, 0f), 1e-4f, "얼룩 위에서 문지른 거리");
            Assert.AreEqual(0f, StainRules.Scrubbed(a + Vector3.up * 0.3f, b + Vector3.up * 0.3f, 0.1f, Center, 0.3f, 0f), "들고만 있으면 0");
            Assert.AreEqual(0f, StainRules.Scrubbed(a + Vector3.right, b + Vector3.right, 0.1f, Center, 0.3f, 0f), "얼룩 밖이면 0");
            Assert.AreEqual(0f, StainRules.Scrubbed(a, a + new Vector3(0.01f, 0f, 0f), 0.1f, Center, 0.3f, 0f), "너무 느리면(대고만 있음) 0");
            Assert.AreEqual(0f, StainRules.Scrubbed(a, b, 0f, Center, 0.3f, 0f), "시간이 안 흐르면 0");
        }

        [Test]
        public void Remaining_DropsByScrubbedShare_AndClamps()
        {
            Assert.AreEqual(0.5f, StainRules.Remaining(1f, 0.6f, 1.2f), 1e-4f);
            Assert.AreEqual(0f, StainRules.Remaining(0.1f, 1f, 1.2f));
            Assert.AreEqual(1f, StainRules.Remaining(1f, 0f, 1.2f));
        }
    }
}
