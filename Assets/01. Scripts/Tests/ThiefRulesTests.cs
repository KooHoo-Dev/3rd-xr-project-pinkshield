using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>카드 도둑 규칙 (카드도둑_설계 3 · 4장).</summary>
    public class ThiefRulesTests
    {
        static CustomerRoutes ShopRoutes() => new CustomerRoutes(
            ShopLayout.OutsidePos, ShopLayout.DoorOutsidePos, ShopLayout.DoorInsidePos,
            ShopLayout.EntryTurnPos, ShopLayout.AisleLowPos, ShopLayout.CounterExitPos);

        [Test]
        public void CountToday_StartsOnFirstDay()
        {
            Assert.AreEqual(0, ThiefRules.CountToday(1, 3, 1));
            Assert.AreEqual(0, ThiefRules.CountToday(2, 3, 1));
            Assert.AreEqual(1, ThiefRules.CountToday(3, 3, 1));
            Assert.AreEqual(1, ThiefRules.CountToday(7, 3, 1));
            Assert.AreEqual(0, ThiefRules.CountToday(5, 3, -2), "음수 횟수는 0");
        }

        [Test]
        public void DefaultConfig_ThiefFromDay3_OncePerDay()
        {
            var config = ScriptableObject.CreateInstance<CampaignConfig>();
            for (int day = 1; day <= config.DayCount; day++)
                Assert.AreEqual(day >= 3 ? 1 : 0, ThiefRules.CountToday(day, config.thief.firstDay, config.thief.perDay), $"{day}일차");
            Assert.Less(config.thief.runSpeed, 2.5f, "도둑은 플레이어 이동(2.5 m/s)보다 느려야 함");
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Schedule_StaysInsideWindow_AndIsSorted()
        {
            var rng = new SystemRandomSource(5);
            for (int i = 0; i < 200; i++)
            {
                float[] t = ThiefRules.Schedule(3, 135f, new Vector2(0.25f, 0.6f), rng);
                Assert.AreEqual(3, t.Length);
                for (int k = 0; k < t.Length; k++)
                {
                    Assert.GreaterOrEqual(t[k], 135f * 0.25f - 1e-3f);
                    Assert.LessOrEqual(t[k], 135f * 0.6f + 1e-3f);
                    if (k > 0) Assert.GreaterOrEqual(t[k], t[k - 1]);
                }
            }
            Assert.IsEmpty(ThiefRules.Schedule(0, 135f, new Vector2(0.25f, 0.6f), rng));
            float[] flipped = ThiefRules.Schedule(1, 100f, new Vector2(0.6f, 0.25f), rng);
            Assert.That(flipped[0], Is.InRange(25f, 60f), "최소 · 최대가 뒤집혀도 같은 구간");
        }

        [Test]
        public void IsHit_NeedsSwingSpeed_AndBody()
        {
            Vector3 feet = new Vector3(1f, 0f, 2f);
            Vector3 chest = feet + new Vector3(0.2f, 1.2f, 0f);
            Assert.IsTrue(ThiefRules.IsHit(chest, 2f, feet, 1.7f, 0.35f, 1f), "휘둘러서 몸에 닿음");
            Assert.IsFalse(ThiefRules.IsHit(chest, 0.5f, feet, 1.7f, 0.35f, 1f), "톡 댄 것은 안 침");
            Assert.IsFalse(ThiefRules.IsHit(feet + new Vector3(0.5f, 1f, 0f), 2f, feet, 1.7f, 0.35f, 1f), "몸 옆을 스침");
            Assert.IsFalse(ThiefRules.IsHit(feet + new Vector3(0f, 2.2f, 0f), 2f, feet, 1.7f, 0.35f, 1f), "머리 위 허공");
            Assert.IsTrue(ThiefRules.IsHit(feet + new Vector3(0f, 0.1f, 0.1f), 2f, feet, 1.7f, 0.35f, 1f), "다리를 쓸어도 맞음");
        }

        [Test]
        public void FreePacksToday_ZeroOnlyWhenStolen()
        {
            Assert.AreEqual(1, ThiefRules.FreePacksToday(1, false));
            Assert.AreEqual(0, ThiefRules.FreePacksToday(1, true));
            Assert.AreEqual(2, ThiefRules.FreePacksToday(2, false));
        }

        [Test]
        public void EscapePoint_IsOnWalkableSidewalk_AboutTenMetersFromDoor()
        {
            foreach (bool left in new[] { true, false })
            {
                Vector3 p = ThiefRules.EscapePoint(ShopLayout.DoorOutsidePos, 6.4f, 10f, left);
                Assert.IsTrue(ShopLayout.InsideWalkArea(p, chasing: true), $"도망 지점 {p}이 플레이어가 갈 수 없는 곳 (쫓아갈 수 없음)");
                if (!left) Assert.IsFalse(ShopLayout.InsideWalkArea(p), $"평소에도 도망 지점 {p}까지 걸어갈 수 있음 (도로 끝 제한 A8이 풀림)");
                Assert.AreEqual(10f, Mathf.Abs(p.x - ShopLayout.DoorOutsidePos.x), 1e-4f);
            }
        }

        [Test]
        public void FleePath_LeavesThroughDoor_AndTakesAboutTenToFifteenSeconds()
        {
            CustomerRoutes routes = ShopRoutes();
            Vector3 steal = new Vector3(-0.95f, 0f, 1.4f);
            Vector3 escape = ThiefRules.EscapePoint(routes.DoorOutside, 6.4f, 10f, false);
            Vector3[] path = ThiefRules.FleePath(routes, escape);
            CollectionAssert.Contains(path, routes.Door, "문으로 나가야 함 (벽 뚫기 없음)");
            Assert.AreEqual(escape, path[path.Length - 1]);

            float length = 0f;
            Vector3 at = steal;
            foreach (Vector3 p in path) { length += Vector3.Distance(at, p); at = p; }
            float seconds = length / new CampaignConfig.ThiefSettings().runSpeed;
            Assert.That(seconds, Is.InRange(8f, 15f), $"추격 시간 {seconds:0.0}초 (DEC-015: 10~15초 안에 끝)");
        }

        [Test]
        public void RunOffPath_FromInsideShop_GoesThroughDoor()
        {
            CustomerRoutes routes = ShopRoutes();
            Vector3 far = ThiefRules.EscapePoint(routes.DoorOutside, 6.4f, 22f, true);
            Vector3[] nearCounter = ThiefRules.RunOffPath(routes, new Vector3(-0.9f, 0f, 1.4f), true, far);
            CollectionAssert.Contains(nearCounter, routes.EntryTurn, "카운터 앞에서는 통로를 따라 나감 (줄 · 진열대 가로지르지 않음)");
            CollectionAssert.Contains(nearCounter, routes.Door);
            Vector3[] nearDoor = ThiefRules.RunOffPath(routes, new Vector3(2.5f, 0f, 3.6f), true, far);
            Assert.AreEqual(routes.Door, nearDoor[0]);
            Vector3[] outside = ThiefRules.RunOffPath(routes, new Vector3(5f, 0f, 6f), false, far);
            CollectionAssert.DoesNotContain(outside, routes.Door, "밖에서는 가게로 다시 들어가지 않음");
        }
    }
}
