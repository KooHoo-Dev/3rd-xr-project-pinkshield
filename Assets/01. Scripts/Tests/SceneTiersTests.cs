using CardShop.Cards;
using CardShop.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>씬에 놓은 단계 가구 고르기 (SceneTiers, 11-20 플레이 F5: Bed_Tier_0 · 1 · 2).</summary>
    public class SceneTiersTests
    {
        [Test]
        public void Pick_LevelMinusOne_OrHighestBelow()
        {
            int[] all = { 0, 1, 2 };
            Assert.AreEqual(0, SceneTiers.Pick(all, 1), "1단계 = Tier_0");
            Assert.AreEqual(1, SceneTiers.Pick(all, 2));
            Assert.AreEqual(2, SceneTiers.Pick(all, 3));
            Assert.AreEqual(2, SceneTiers.Pick(all, 9), "넘치면 가장 높은 것");
            Assert.AreEqual(0, SceneTiers.Pick(new[] { 0, 2 }, 2), "그 단계가 없으면 아래에서 가장 높은 것");
            Assert.AreEqual(1, SceneTiers.Pick(new[] { 1, 2 }, 1), "아래에 없으면 가장 낮은 것");
            Assert.AreEqual(-1, SceneTiers.Pick(new int[0], 1));
        }

        [Test]
        public void Apply_TurnsOnlyMatchingTierOn_IgnoresOtherKinds()
        {
            var interior = new GameObject("HomeInterior").transform;
            try
            {
                GameObject t0 = Child(interior, "Bed_Tier_0"), t1 = Child(interior, "Bed_Tier_1"), t2 = Child(interior, "Bed_Tier_2");
                GameObject other = Child(interior, "Rug_Tier_1");
                t1.SetActive(false);
                t2.SetActive(false);

                Assert.IsTrue(SceneTiers.Apply(interior, UpgradeKind.Bed, 2));
                Assert.IsFalse(t0.activeSelf);
                Assert.IsTrue(t1.activeSelf);
                Assert.IsFalse(t2.activeSelf);
                Assert.IsTrue(other.activeSelf, "다른 분야 Tier는 그대로");

                Assert.IsTrue(SceneTiers.Apply(interior, UpgradeKind.Bed, 1), "새 게임: 처음으로");
                Assert.IsTrue(t0.activeSelf);
                Assert.IsFalse(t1.activeSelf);

                Assert.IsFalse(SceneTiers.Apply(interior, UpgradeKind.Computer, 2), "Tier가 없는 분야는 카탈로그대로");
            }
            finally { Object.DestroyImmediate(interior.gameObject); }
        }

        static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
