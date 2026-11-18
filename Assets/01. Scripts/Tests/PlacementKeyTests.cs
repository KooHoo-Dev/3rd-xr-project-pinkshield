using CardShop.Core;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>
    /// 씬에서 손으로 옮긴 오브젝트 판별 (PlacementKey, 2026-11-18 기기 테스트 피드백).
    /// Build Shop Scene은 "빌더 기본 자리에서 옮겨진 것"만 기억하므로, 판별이 틀리면 손으로 옮긴 자리가 사라지거나
    /// 반대로 빌더 기본값을 고쳐도 반영되지 않습니다.
    /// </summary>
    public class PlacementKeyTests
    {
        GameObject parent, child;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("Checkout");
            child = new GameObject("PaperBag");
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = new Vector3(0.5f, 0.9f, 0.5f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(parent);

        [Test]
        public void JustBuilt_IsNotMoved()
        {
            PlacementKey key = PlacementKey.Mark(child, "Counter/PaperBag");
            key.RecordDefault();
            Assert.IsFalse(key.IsMoved);
        }

        [Test]
        public void MovedRotatedOrScaled_IsMoved()
        {
            PlacementKey key = PlacementKey.Mark(child, "Counter/PaperBag");
            key.RecordDefault();
            child.transform.localPosition += new Vector3(0.01f, 0f, 0f);
            Assert.IsTrue(key.IsMoved, "1cm 옮김");

            key.RecordDefault();
            child.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
            Assert.IsTrue(key.IsMoved, "15도 돌림");

            key.RecordDefault();
            child.transform.localScale = Vector3.one * 1.2f;
            Assert.IsTrue(key.IsMoved, "1.2배");
        }

        [Test]
        public void MovingTheParent_KeepsTheChildUnmoved()
        {
            // 계산대 묶음(부모)을 옮기면 봉투(자식)는 함께 따라가고, 봉투 자체는 "옮긴 것"이 아님 (로컬 기준 비교)
            PlacementKey bag = PlacementKey.Mark(child, "Counter/PaperBag");
            PlacementKey counter = PlacementKey.Mark(parent, "Counter/Checkout");
            bag.RecordDefault();
            counter.RecordDefault();

            parent.transform.position += new Vector3(0.3f, 0f, -0.2f);
            Assert.IsTrue(counter.IsMoved);
            Assert.IsFalse(bag.IsMoved);
        }
    }
}
