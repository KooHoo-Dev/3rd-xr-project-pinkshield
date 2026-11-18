using System.Collections.Generic;
using CardShop.Core;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>
    /// 가게 배치 기준값 검사 (가게_집_인테리어_설계 2장). 손님은 웨이포인트 사이를 직선으로 걸으므로
    /// 경로 선분이 벽을 지나면 손님이 벽을 뚫고 걷습니다. 인테리어 프리팹 · ShopLayout 값을 고친 뒤 여기서 바로 확인합니다.
    /// </summary>
    public class ShopLayoutTests
    {
        // 위에서 본 2D (x, z) 선분
        struct Seg { public Vector2 a, b; public Seg(Vector2 a, Vector2 b) { this.a = a; this.b = b; } }

        static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);

        // 출입구 구멍을 뺀 벽 선분들 (벽 중심선)
        static List<Seg> Walls() => new List<Seg>
        {
            new Seg(new Vector2(ShopLayout.MinX, ShopLayout.BackZ), new Vector2(ShopLayout.MaxX, ShopLayout.BackZ)),
            new Seg(new Vector2(ShopLayout.MinX, ShopLayout.BackZ), new Vector2(ShopLayout.MinX, ShopLayout.FrontZ)),
            new Seg(new Vector2(ShopLayout.MaxX, ShopLayout.BackZ), new Vector2(ShopLayout.MaxX, ShopLayout.FrontZ)),
            new Seg(new Vector2(ShopLayout.MinX, ShopLayout.FrontZ), new Vector2(ShopLayout.DoorX - ShopLayout.DoorHalfWidth, ShopLayout.FrontZ)),
            new Seg(new Vector2(ShopLayout.DoorX + ShopLayout.DoorHalfWidth, ShopLayout.FrontZ), new Vector2(ShopLayout.MaxX, ShopLayout.FrontZ)),
        };

        static bool Crosses(Seg p, Seg q)
        {
            float D(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            float d1 = D(q.a, q.b, p.a), d2 = D(q.a, q.b, p.b), d3 = D(p.a, p.b, q.a), d4 = D(p.a, p.b, q.b);
            return d1 * d2 < 0f && d3 * d4 < 0f;
        }

        static void AssertPathClear(string name, params Vector3[] points)
        {
            for (int i = 0; i + 1 < points.Length; i++)
            {
                var s = new Seg(XZ(points[i]), XZ(points[i + 1]));
                foreach (Seg w in Walls())
                    Assert.IsFalse(Crosses(s, w), $"{name}: {points[i]} → {points[i + 1]} 구간이 벽({w.a}~{w.b})을 지납니다");
            }
        }

        // ------------------------------------------------------------------ 손님 길 (CustomerRoutes)

        /// <summary>하루 손님 최대 6명(CampaignConfig) → 줄은 최대 6칸(0~5번)까지 확인</summary>
        const int MaxQueue = 6;
        /// <summary>손님 몸 반지름 정도. 지나가는 길이 줄 선 손님 중심에서 이보다 가까우면 몸이 겹쳐 보임</summary>
        const float BodyGap = 0.45f;

        static CustomerRoutes Routes => new CustomerRoutes(ShopLayout.OutsidePos, ShopLayout.DoorOutsidePos, ShopLayout.DoorInsidePos,
            ShopLayout.EntryTurnPos, ShopLayout.AisleLowPos, ShopLayout.CounterExitPos);

        static Vector3 Slot(int index) => CustomerRoutes.SlotPosition(ShopLayout.QueuePositions, Vector3.back, index);

        // 손님이 걸을 수 있는 모든 길 (시작점 포함)
        static IEnumerable<(string name, Vector3[] path)> AllPaths()
        {
            CustomerRoutes r = Routes;
            foreach (Vector3 browse in ShopLayout.BrowsePositions)
            {
                yield return ($"입장 → 구경 {browse}", Prepend(ShopLayout.OutsidePos, r.EnterToBrowse(browse)));
                for (int k = 0; k < MaxQueue; k++)
                    yield return ($"구경 {browse} → 줄 {k}번", Prepend(browse, r.BrowseToQueue(browse, Slot(k))));
            }
            for (int k = 0; k < MaxQueue; k++)
            {
                yield return ($"입장 → 바로 줄 {k}번", Prepend(ShopLayout.OutsidePos, r.EnterToQueue(Slot(k))));
                if (k > 0) yield return ($"줄 {k}번에서 떠남", Prepend(Slot(k), r.LeaveFromQueue(Slot(k), Slot(0))));
            }
            yield return ("결제 후 퇴장", Prepend(Slot(0), r.LeaveFromCounter()));
        }

        static Vector3[] Prepend(Vector3 first, Vector3[] rest)
        {
            var all = new Vector3[rest.Length + 1];
            all[0] = first;
            rest.CopyTo(all, 1);
            return all;
        }

        static float DistanceToPath(Vector3 point, Vector3[] path, int fromSegment = 0)
        {
            float best = float.MaxValue;
            Vector2 p = XZ(point);
            for (int i = fromSegment; i + 1 < path.Length; i++)
            {
                Vector2 a = XZ(path[i]), b = XZ(path[i + 1]);
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        [Test]
        public void CustomerPaths_DoNotCrossWalls()
        {
            foreach ((string name, Vector3[] path) in AllPaths()) AssertPathClear(name, path);
        }

        [Test]
        public void CustomerPaths_DoNotWalkThroughCounterOrStands()
        {
            const float body = 0.2f;
            var blocks = new[]
            {
                ("카운터", Footprint(ShopLayout.CounterCenter, ShopLayout.CounterSize + new Vector3(2f * body, 0f, 2f * body))),
                ("팩 테이블", Footprint(ShopLayout.PackTablePos, ShopLayout.PackTableSize + new Vector3(2f * body, 0f, 2f * body))),
                ("판매 진열대", Footprint(ShopLayout.StandPos, ShopLayout.StandSize + new Vector3(2f * body, 0f, 2f * body))),
            };
            foreach ((string name, Vector3[] path) in AllPaths())
                for (int i = 0; i + 1 < path.Length; i++)
                    for (float t = 0f; t <= 1f; t += 0.02f)
                    {
                        Vector2 p = Vector2.Lerp(XZ(path[i]), XZ(path[i + 1]), t);
                        foreach ((string block, Rect rect) in blocks)
                            Assert.IsFalse(rect.Contains(p), $"{name}: {path[i]} → {path[i + 1]} 구간이 {block}을(를) 지납니다 ({p})");
                    }
        }

        [Test]
        public void QueueSlots_AreSpacedApart()
        {
            for (int i = 0; i < MaxQueue; i++)
                for (int j = i + 1; j < MaxQueue; j++)
                    Assert.GreaterOrEqual(Vector3.Distance(Slot(i), Slot(j)), 0.6f, $"줄 {i}번 · {j}번 칸이 너무 붙어 손님이 겹칩니다");
            for (int i = 0; i < MaxQueue; i++)
                Assert.IsTrue(ShopLayout.InsideShop(Slot(i), 0.3f), $"줄 {i}번 칸 {Slot(i)} 가 벽에 너무 붙음");
        }

        [Test]
        public void JoiningCustomer_ApproachesTheLineFromBehind()
        {
            // 줄 끝(k번)으로 오는 손님의 길이 이미 줄 선 손님(0 ~ k-1번)을 뚫고 지나가지 않음
            CustomerRoutes r = Routes;
            for (int k = 0; k < MaxQueue; k++)
                for (int ahead = 0; ahead < k; ahead++)
                {
                    foreach (Vector3 browse in ShopLayout.BrowsePositions)
                        Assert.GreaterOrEqual(DistanceToPath(Slot(ahead), Prepend(browse, r.BrowseToQueue(browse, Slot(k)))), BodyGap,
                            $"구경 → 줄 {k}번 길이 {ahead}번 손님을 지나감");
                    Assert.GreaterOrEqual(DistanceToPath(Slot(ahead), r.EnterToQueue(Slot(k))), BodyGap, $"입장 → 줄 {k}번 길이 {ahead}번 손님을 지나감");
                }
        }

        [Test]
        public void EnteringAndPaidCustomers_DoNotWalkThroughTheLine()
        {
            CustomerRoutes r = Routes;
            for (int k = 0; k < MaxQueue; k++)
            {
                foreach (Vector3 browse in ShopLayout.BrowsePositions)
                    Assert.GreaterOrEqual(DistanceToPath(Slot(k), r.EnterToBrowse(browse)), BodyGap, $"입장 길이 줄 {k}번 손님을 지나감");
                if (k > 0)
                    Assert.GreaterOrEqual(DistanceToPath(Slot(k), Prepend(Slot(0), r.LeaveFromCounter())), BodyGap, $"결제 후 나가는 길이 줄 {k}번 손님을 지나감");
            }
        }

        [Test]
        public void Waypoints_AreOnTheRightSideOfTheWalls()
        {
            Assert.IsFalse(ShopLayout.InsideShop(ShopLayout.OutsidePos), "밖 지점이 가게 안에 있음");
            Assert.IsFalse(ShopLayout.InsideShop(ShopLayout.DoorOutsidePos), "문 바깥 지점이 가게 안에 있음");
            Assert.IsTrue(ShopLayout.InsideShop(ShopLayout.DoorInsidePos, 0.2f), "문 안 지점이 가게 밖이거나 벽에 붙음");
            foreach (Vector3 p in new[] { ShopLayout.EntryTurnPos, ShopLayout.AisleLowPos, ShopLayout.CounterExitPos })
                Assert.IsTrue(ShopLayout.InsideShop(p, 0.3f), $"통로 지점 {p} 가 벽에 너무 붙음");
            foreach (Vector3 p in ShopLayout.BrowsePositions) Assert.IsTrue(ShopLayout.InsideShop(p, 0.3f), $"구경 자리 {p} 가 벽에 너무 붙음");
            foreach (Vector3 p in ShopLayout.QueuePositions) Assert.IsTrue(ShopLayout.InsideShop(p, 0.3f), $"줄 자리 {p} 가 벽에 너무 붙음");
        }

        [Test]
        public void CustomerSpawn_IsHiddenFromCounter()
        {
            // 계산대(원점)에서 등장 지점을 잇는 시선이 앞벽을 지나는 곳이 출입구 · 파노라마 창이 아닌 막힌 벽이어야 함
            Vector3 o = ShopLayout.OutsidePos;
            float xAtFront = o.x * ShopLayout.FrontZ / o.z;
            Assert.Less(xAtFront, ShopLayout.MaxX, "시선이 앞벽이 아니라 오른쪽 벽 쪽으로 빠짐");
            Assert.Greater(xAtFront, ShopLayout.DoorX + ShopLayout.DoorHalfWidth, "등장 지점이 출입구로 보입니다");
            Assert.Greater(xAtFront, ShopLayout.PanoramaMaxX, "등장 지점이 창으로 보입니다");
        }

        [Test]
        public void Counter_PackTable_Stand_DoNotOverlap()
        {
            Rect counter = Footprint(ShopLayout.CounterCenter, ShopLayout.CounterSize);
            Rect table = Footprint(ShopLayout.PackTablePos, ShopLayout.PackTableSize);
            Rect stand = Footprint(ShopLayout.StandPos, ShopLayout.StandSize);
            Assert.IsFalse(counter.Overlaps(table), "카운터와 팩 테이블이 겹침");
            Assert.IsFalse(counter.Overlaps(stand), "카운터와 판매 진열대가 겹침");
            Assert.IsFalse(table.Overlaps(stand), "팩 테이블과 판매 진열대가 겹침");
            foreach (Vector3 p in ShopLayout.BrowsePositions) Assert.IsFalse(stand.Contains(XZ(p)), $"구경 자리 {p} 가 진열대 안");
            foreach (Vector3 p in ShopLayout.QueuePositions) Assert.IsFalse(counter.Contains(XZ(p)), $"줄 자리 {p} 가 카운터 안");
        }

        [Test]
        public void ShellModules_FitTheWalls()
        {
            // 껍데기는 2.5m 모듈 × 배율로 가로 3칸 · 세로 2칸이 정확히 맞아야 벽 사이 틈이 없음
            Assert.AreEqual(3f * ShopLayout.Module, ShopLayout.MaxX - ShopLayout.MinX, 1e-4f);
            Assert.AreEqual(2f * ShopLayout.Module, ShopLayout.FrontZ - ShopLayout.BackZ, 1e-4f);
            Assert.AreEqual(ShopLayout.MaxX - ShopLayout.Module / 2f, ShopLayout.DoorX, 1e-4f, "출입구가 앞벽 오른쪽 칸 가운데가 아님");
        }

        /// <summary>타이틀 자리는 걸을 수 있는 인도이고, 타이틀 판은 그 앞(가게 쪽) 인도 위, 플레이어와 가게 벽 사이에 있음</summary>
        [Test]
        public void TitleSpot_IsOnSidewalk_FacingShop()
        {
            Assert.IsTrue(ShopLayout.InsideWalkArea(ShopLayout.TitleSpawnPos), "타이틀 자리가 걸을 수 있는 곳이 아님 (PlayAreaGuard가 밀어냄)");
            Assert.IsFalse(ShopLayout.InsideShop(ShopLayout.TitleSpawnPos), "타이틀 자리는 가게 밖이어야 함");
            Assert.Less(ShopLayout.TitleBoardPos.z, ShopLayout.TitleSpawnPos.z, "판이 플레이어 뒤에 있음 (플레이어는 −Z를 봄)");
            Assert.Greater(ShopLayout.TitleBoardPos.z, ShopLayout.FrontZ + 0.5f, "판이 가게 벽에 너무 붙음");
            Assert.AreEqual(180f, ShopLayout.TitleSpawnYaw);
        }

        /// <summary>정산 판 아래 끝(판 높이 0.62m)이 카운터 윗면보다 위 (BUG-010: 패널이 카운터에 묻힘)</summary>
        [Test]
        public void ResultBoard_IsAboveCounter()
        {
            Assert.Greater(ShopLayout.ResultBoardPos.y - 0.31f, ShopLayout.CounterTop);
        }

        static Rect Footprint(Vector3 center, Vector3 size) =>
            new Rect(center.x - size.x / 2f, center.z - size.z / 2f, size.x, size.z);
    }
}
