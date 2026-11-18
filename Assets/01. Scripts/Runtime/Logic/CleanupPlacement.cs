using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Logic
{
    /// <summary>
    /// 청소 구역(쓰레기 · 떨어진 팩)이 생길 바닥 자리 고르기 (순수 계산, 3차 기기 피드백).
    /// - 플레이어가 지금 보고 있는 쪽(시야 부채꼴) 위주. 가끔(outOfViewChance)은 시야 밖도 허용해 둘러보게 함
    /// - 이미 있는 청소 구역과 minSeparation 이상 떨어뜨려 한곳에 몰리지 않게
    /// - 바닥 영역(area) 안, 가구 상자(obstacles) 밖
    /// 조건을 다 만족하는 자리를 못 찾으면 조건을 하나씩 풀어 가장 나은 자리를 돌려줍니다(항상 영역 안).
    /// </summary>
    public sealed class CleanupPlacement
    {
        /// <summary>바닥 영역 (x, z). y는 무시</summary>
        public Rect Area { get; set; }
        /// <summary>피할 가구 영역 (x, z, 이미 여유를 더한 크기)</summary>
        public List<Rect> Obstacles { get; } = new List<Rect>();
        /// <summary>시야 부채꼴 반각 (도). Quest 시야(약 90~100도)보다 좁게 잡아 화면 가운데 쪽으로</summary>
        public float ViewHalfAngle { get; set; } = 35f;
        public float MinViewDistance { get; set; } = 0.8f;
        public float MaxViewDistance { get; set; } = 4.5f;
        /// <summary>청소 구역끼리 최소 거리 (m)</summary>
        public float MinSeparation { get; set; } = 0.9f;
        /// <summary>시야 밖에 놓아도 되는 확률 (0~1). "예외 쓰레기"</summary>
        public float OutOfViewChance { get; set; } = 0.15f;
        public int Tries { get; set; } = 40;

        public CleanupPlacement(Rect area) => Area = area;

        /// <summary>
        /// 자리를 고릅니다. viewerForward는 수평 성분만 씁니다(고개를 숙여도 같은 방향).
        /// </summary>
        public Vector3 Pick(IRandomSource random, Vector3 viewerPos, Vector3 viewerForward, IReadOnlyList<Vector3> occupied)
        {
            bool allowOutOfView = random.NextDouble() < OutOfViewChance;
            Vector3 best = Sample(random);
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < Tries; i++)
            {
                Vector3 p = Sample(random);
                if (HitsObstacle(p)) continue;
                bool inView = InView(p, viewerPos, viewerForward);
                float gap = NearestGap(p, occupied);
                bool separated = gap >= MinSeparation;
                if (separated && (inView || allowOutOfView)) return p;

                // 못 찾을 때를 대비한 점수: 시야 안 > 떨어진 거리
                float score = (inView ? 100f : 0f) + Mathf.Min(gap, 10f);
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        public bool InView(Vector3 p, Vector3 viewerPos, Vector3 viewerForward)
        {
            Vector3 to = p - viewerPos;
            to.y = 0f;
            Vector3 fwd = viewerForward;
            fwd.y = 0f;
            float dist = to.magnitude;
            if (dist < MinViewDistance || dist > MaxViewDistance) return false;
            if (fwd.sqrMagnitude < 1e-6f) return true; // 방향을 모르면 거리만 봄
            return Vector3.Angle(fwd, to) <= ViewHalfAngle;
        }

        public bool HitsObstacle(Vector3 p)
        {
            var xz = new Vector2(p.x, p.z);
            foreach (Rect r in Obstacles) if (r.Contains(xz)) return true;
            return false;
        }

        static float NearestGap(Vector3 p, IReadOnlyList<Vector3> occupied)
        {
            float best = float.PositiveInfinity;
            if (occupied == null) return best;
            foreach (Vector3 o in occupied)
            {
                float d = new Vector2(p.x - o.x, p.z - o.z).magnitude;
                if (d < best) best = d;
            }
            return best;
        }

        Vector3 Sample(IRandomSource random)
        {
            float x = Area.xMin + (float)random.NextDouble() * Area.width;
            float z = Area.yMin + (float)random.NextDouble() * Area.height;
            return new Vector3(x, 0f, z);
        }

        /// <summary>가운데 (cx, cz), 크기 (w, d)인 상자에 여유(margin)를 더한 Rect.</summary>
        public static Rect Box(Vector3 center, Vector3 size, float margin) =>
            new Rect(center.x - size.x / 2f - margin, center.z - size.z / 2f - margin, size.x + 2f * margin, size.z + 2f * margin);
    }
}
