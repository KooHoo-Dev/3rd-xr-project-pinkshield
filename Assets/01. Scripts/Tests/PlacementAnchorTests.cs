using CardShop.Core;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>
    /// 맵 수정 도구의 자리표 · 손으로 붙인 이름표 (맵_수정_도구_설계).
    /// 자리표가 없을 때 기본 자리로 돌아가지 않으면 기존 게임이 깨지고, 계층 경로를 못 찾으면 Build Shop Scene 뒤 자리가 사라집니다.
    /// </summary>
    public class PlacementAnchorTests
    {
        GameObject root;

        [SetUp]
        public void SetUp() => root = new GameObject("PlacementTestRoot");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static readonly Pose Fallback = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));

        [Test]
        public void NoAnchor_UsesFallback()
        {
            Pose p = PlacementAnchor.Find("Test/None", Fallback);
            Assert.AreEqual(Fallback.position, p.position);
        }

        [Test]
        public void Anchor_UsesItsWorldPose_EvenWhenInactive()
        {
            var go = new GameObject("Anchor");
            go.transform.SetParent(root.transform);
            go.transform.SetPositionAndRotation(new Vector3(-2f, 1f, 0.5f), Quaternion.Euler(0f, 30f, 0f));
            go.AddComponent<PlacementAnchor>().key = "Test/Sign";
            root.SetActive(false);

            Pose p = PlacementAnchor.Find("Test/Sign", Fallback);
            Assert.That(Vector3.Distance(p.position, new Vector3(-2f, 1f, 0.5f)), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(p.rotation, Quaternion.Euler(0f, 30f, 0f)), Is.LessThan(0.01f));
        }

        [Test]
        public void HierarchyPath_RoundTrips_IncludingInactive()
        {
            var a = new GameObject("A").transform;
            var b = new GameObject("B").transform;
            a.SetParent(root.transform);
            b.SetParent(a);
            a.gameObject.SetActive(false);

            string path = HierarchyPath.Of(b);
            Assert.AreEqual("PlacementTestRoot/A/B", path);
            Assert.AreSame(b, HierarchyPath.Find(root.scene, path));
            Assert.IsNull(HierarchyPath.Find(root.scene, "PlacementTestRoot/A/Missing"));
        }

        [Test]
        public void MarkByHand_IsAlwaysRemembered_BuilderMarkOnlyWhenMoved()
        {
            var hand = new GameObject("Lamp");
            hand.transform.SetParent(root.transform);
            PlacementKey byHand = PlacementKey.MarkByHand(hand);
            Assert.AreEqual("PlacementTestRoot/Lamp", byHand.key);
            Assert.IsFalse(byHand.IsMoved);
            Assert.IsTrue(byHand.ShouldRemember);

            var built = new GameObject("Bag");
            built.transform.SetParent(root.transform);
            PlacementKey builder = PlacementKey.Mark(built, "Counter/PaperBag");
            builder.RecordDefault();
            Assert.IsFalse(builder.ShouldRemember);
            built.transform.localPosition += Vector3.right * 0.1f;
            Assert.IsTrue(builder.ShouldRemember);
        }
    }
}
