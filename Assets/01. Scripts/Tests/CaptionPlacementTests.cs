using CardShop.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>튜토리얼 자막 자리 (11-20 "자막이 가끔 안 보임"): 혼잣말처럼 정면 고정 + 시야 밖이면 다시 놓기</summary>
    public class CaptionPlacementTests
    {
        [Test]
        public void PitchFor_LookingStraight_IsSlightlyBelow()
        {
            Assert.AreEqual(CaptionPlacement.PitchDown, CaptionPlacement.PitchFor(0f), 0.001f);
        }

        [Test]
        public void PitchFor_LookingDown_StaysAtHorizonOffset_SoHandsAreNotCovered()
        {
            Assert.AreEqual(CaptionPlacement.PitchDown, CaptionPlacement.PitchFor(45f), 0.001f);
        }

        [Test]
        public void PitchFor_LookingUp_FollowsGaze_UpToLimit()
        {
            Assert.AreEqual(-30f + CaptionPlacement.PitchDown, CaptionPlacement.PitchFor(-30f), 0.001f);
            Assert.AreEqual(-CaptionPlacement.MaxPitchUp, CaptionPlacement.PitchFor(-80f), 0.001f);
        }

        [Test]
        public void Front_IsLeftOfCenter() // 11-20: 쥔 물건(가운데 ~ 오른손)을 비켜 왼쪽
        {
            Pose p = CaptionPlacement.Front(Vector3.zero, Vector3.forward);
            Assert.Less(p.position.x, -0.1f);
        }

        [Test]
        public void Front_IsAtDistance_FacingAwayFromHead()
        {
            Vector3 head = new Vector3(1f, 1.6f, 2f);
            Pose p = CaptionPlacement.Front(head, Vector3.forward);
            Assert.AreEqual(CaptionPlacement.Distance, Vector3.Distance(head, p.position), 0.001f);
            Assert.Less(p.position.y, head.y);                          // 조금 아래
            Assert.Greater(Vector3.Dot(p.rotation * Vector3.forward, Vector3.forward), 0.9f); // 앞면(-Z)이 플레이어 쪽
        }

        [Test]
        public void Front_LookingUp_IsStillInView()
        {
            Vector3 head = new Vector3(0f, 1.6f, 0f);
            Vector3 gaze = Quaternion.Euler(-45f, 0f, 0f) * Vector3.forward; // 벽시계를 올려다봄
            Pose p = CaptionPlacement.Front(head, gaze);
            Assert.IsFalse(CaptionPlacement.IsOutOfView(head, gaze, p.position));
        }

        [Test]
        public void IsOutOfView_TurnedAround_True_GlanceAside_False()
        {
            Vector3 head = Vector3.zero;
            Pose p = CaptionPlacement.Front(head, Vector3.forward);
            Assert.IsFalse(CaptionPlacement.IsOutOfView(head, Quaternion.Euler(0f, 30f, 0f) * Vector3.forward, p.position));
            Assert.IsTrue(CaptionPlacement.IsOutOfView(head, Vector3.back, p.position));
        }

        [Test]
        public void WalkedAway_IgnoresHeight_CountsHorizontal()
        {
            Vector3 from = Vector3.zero;
            Assert.IsFalse(CaptionPlacement.WalkedAway(from, new Vector3(0f, 3f, 1f)));
            Assert.IsTrue(CaptionPlacement.WalkedAway(from, new Vector3(1.2f, 0f, 1.2f)));
        }
    }
}
