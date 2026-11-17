using System.Collections.Generic;
using CardShop.Core;
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

        [Test]
        public void CustomerPaths_DoNotCrossWalls()
        {
            Vector3 outside = ShopLayout.OutsidePos, doorOut = ShopLayout.DoorOutsidePos, door = ShopLayout.DoorInsidePos;
            foreach (Vector3 browse in ShopLayout.BrowsePositions)
            {
                AssertPathClear("입장", outside, doorOut, door, browse);
                foreach (Vector3 queue in ShopLayout.QueuePositions)
                    AssertPathClear("구경 → 줄 → 퇴장", browse, queue, door, doorOut, outside);
            }
        }

        [Test]
        public void Waypoints_AreOnTheRightSideOfTheWalls()
        {
            Assert.IsFalse(ShopLayout.InsideShop(ShopLayout.OutsidePos), "밖 지점이 가게 안에 있음");
            Assert.IsFalse(ShopLayout.InsideShop(ShopLayout.DoorOutsidePos), "문 바깥 지점이 가게 안에 있음");
            Assert.IsTrue(ShopLayout.InsideShop(ShopLayout.DoorInsidePos, 0.2f), "문 안 지점이 가게 밖이거나 벽에 붙음");
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

        static Rect Footprint(Vector3 center, Vector3 size) =>
            new Rect(center.x - size.x / 2f, center.z - size.z / 2f, size.x, size.z);
    }
}
