using System.Collections.Generic;
using CardShop.Core;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    public class CleanupPlacementTests
    {
        static CleanupPlacement Make(float outOfView = 0f)
        {
            var p = new CleanupPlacement(Rect.MinMaxRect(-3f, 1f, 3f, 4f)) { OutOfViewChance = outOfView };
            p.Obstacles.Add(Rect.MinMaxRect(-2f, 2f, -1f, 3f));
            return p;
        }

        [Test]
        public void Pick_StaysInAreaAndOutOfObstacles()
        {
            CleanupPlacement p = Make();
            var random = new SystemRandomSource(1);
            for (int i = 0; i < 200; i++)
            {
                Vector3 v = p.Pick(random, new Vector3(0f, 1.6f, 0f), Vector3.forward, null);
                Assert.IsTrue(p.Area.Contains(new Vector2(v.x, v.z)), $"영역 밖: {v}");
                Assert.IsFalse(p.HitsObstacle(v), $"가구 위: {v}");
            }
        }

        [Test]
        public void Pick_PrefersPlayerView()
        {
            CleanupPlacement p = Make(outOfView: 0f);
            var random = new SystemRandomSource(2);
            Vector3 eye = new Vector3(0f, 1.6f, 0f);
            // 오른쪽 앞을 보고 있으면 거의 다 그쪽에 생김
            Vector3 forward = new Vector3(1f, 0f, 1f).normalized;
            int inView = 0;
            for (int i = 0; i < 100; i++)
                if (p.InView(p.Pick(random, eye, forward, null), eye, forward)) inView++;
            Assert.GreaterOrEqual(inView, 95, "시야 안에 생겨야 함");
        }

        [Test]
        public void Pick_KeepsDistanceFromOtherSpots()
        {
            CleanupPlacement p = Make(outOfView: 1f); // 시야 조건 없이 거리 조건만 봄
            var random = new SystemRandomSource(3);
            Vector3 eye = new Vector3(0f, 1.6f, 0f);
            var spots = new List<Vector3>();
            for (int i = 0; i < 5; i++) spots.Add(p.Pick(random, eye, Vector3.forward, spots));
            for (int a = 0; a < spots.Count; a++)
                for (int b = a + 1; b < spots.Count; b++)
                {
                    float d = new Vector2(spots[a].x - spots[b].x, spots[a].z - spots[b].z).magnitude;
                    Assert.GreaterOrEqual(d, p.MinSeparation - 1e-3f, $"{a}번과 {b}번이 너무 가까움 ({d:0.00}m): 한곳에 몰리면 안 됨");
                }
        }

        [Test]
        public void MessyArea_InFrontOfStand_PacksNeverLandUnderIt()
        {
            Rect a = CardShop.Cleanup.CleanupDirector.MessyArea(new Vector2(0.45f, 1.0f));
            float standFront = ShopLayout.StandPos.z - ShopLayout.StandSize.z / 2f;
            Assert.Less(a.yMax + 0.4f, standFront, "팩이 흩어지는 반경(0.4m)까지 더해도 진열대 앞");
            Assert.That(a.center.x, Is.EqualTo(ShopLayout.StandPos.x).Within(0.01f), "진열대 정면");
            foreach (Vector2 c in new[] { a.min, a.max, new Vector2(a.xMin, a.yMax), new Vector2(a.xMax, a.yMin) })
                Assert.IsTrue(ShopLayout.InsideShop(new Vector3(c.x, 0f, c.y)), $"띠 모서리 {c}가 가게 밖");
        }

        [Test]
        public void InView_ChecksAngleAndDistance()
        {
            CleanupPlacement p = Make();
            Vector3 eye = Vector3.zero;
            Assert.IsTrue(p.InView(new Vector3(0f, 0f, 2f), eye, Vector3.forward));
            Assert.IsFalse(p.InView(new Vector3(0f, 0f, -2f), eye, Vector3.forward), "등 뒤");
            Assert.IsFalse(p.InView(new Vector3(2f, 0f, 0.2f), eye, Vector3.forward), "옆");
            Assert.IsFalse(p.InView(new Vector3(0f, 0f, 0.3f), eye, Vector3.forward), "발밑은 안 보임");
        }
    }

    public class CustomerSteeringTests
    {
        [Test]
        public void NoNeighbors_WalksStraight()
        {
            Vector3 v = CustomerSteering.Velocity(Vector3.zero, new Vector3(0f, 0f, 3f), 1f, new List<SteeringNeighbor>());
            Assert.AreEqual(1f, v.magnitude, 1e-4f);
            Assert.Greater(v.z, 0.99f);
        }

        [Test]
        public void HeadOn_BothStepToTheirOwnRight()
        {
            // A는 +Z로, B는 -Z로 마주 걸음 → 서로 반대쪽(월드 기준)으로 비켜 엇갈림
            Vector3 a = Vector3.zero, b = new Vector3(0f, 0f, 0.8f);
            Vector3 va = CustomerSteering.Velocity(a, new Vector3(0f, 0f, 3f), 1f, new[] { new SteeringNeighbor(b, Vector3.back) });
            Vector3 vb = CustomerSteering.Velocity(b, new Vector3(0f, 0f, -3f), 1f, new[] { new SteeringNeighbor(a, Vector3.forward) });
            Assert.Greater(va.x, 0.1f, "A는 자기 오른쪽(+X)으로");
            Assert.Less(vb.x, -0.1f, "B는 자기 오른쪽(-X)으로");
        }

        [Test]
        public void FollowingCloseBehind_SlowsDown()
        {
            Vector3 me = Vector3.zero;
            var leader = new SteeringNeighbor(new Vector3(0f, 0f, 0.4f), new Vector3(0f, 0f, 0.5f));
            Vector3 v = CustomerSteering.Velocity(me, new Vector3(0f, 0f, 3f), 1f, new[] { leader });
            Assert.Less(v.magnitude, 0.5f, "바로 앞 사람을 밀고 들어가지 않음");
        }

        [Test]
        public void NearArrival_IgnoresNeighbors()
        {
            // 줄 칸 바로 앞(도착 직전)이면 옆 사람이 있어도 그대로 칸으로
            Vector3 v = CustomerSteering.Velocity(Vector3.zero, new Vector3(0f, 0f, 0.3f), 1f,
                new[] { new SteeringNeighbor(new Vector3(0.1f, 0f, 0.5f), Vector3.zero) });
            Assert.AreEqual(0f, v.x, 1e-4f);
        }

        [Test]
        public void PersonBehind_IsIgnored()
        {
            Vector3 v = CustomerSteering.Velocity(Vector3.zero, new Vector3(0f, 0f, 3f), 1f,
                new[] { new SteeringNeighbor(new Vector3(0f, 0f, -0.3f), Vector3.forward) });
            Assert.AreEqual(0f, v.x, 1e-4f);
            Assert.AreEqual(1f, v.z, 1e-4f);
        }
    }
}
