using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 봉투 안 물건 자리 정하기 (11-20 C3 → 11-22 G1 사용자 그림대로). 순수 계산이라 테스트(BagLayoutTests)로 확인합니다.
    /// 입력: 물건마다 크기(가로 x · 높이 y · 깊이 z, m). 출력: 물건 바닥 가운데 자리(봉투 로컬, 바닥 가운데가 원점).
    /// 물건 종류는 모양으로 나눔: 납작한 것(높이 &lt; FlatHeight) = 팩 · 카드, 높이가 가장 긴 변 이하 = 상자, 나머지(키 큰 것) = 피규어.
    /// ① 오른쪽 칸: 상자(바닥) → 팩 → 판매 등록 카드(맨 위) 순서로 쌓음 — 납작한 것은 두꺼운 것(팩)이 먼저, 같으면 들어온 순서
    /// ② 왼쪽 칸: 피규어를 앞뒤로 나란히 (깊이에 다 안 들어가면 그 위 층에)
    /// ③ 피규어가 없으면 쌓은 줄을 가운데에. 상자가 너무 넓어 왼쪽 칸이 없으면 피규어는 쌓은 줄 맨 위
    /// </summary>
    public static class BagLayout
    {
        public const float FlatHeight = 0.03f;
        public const float DefaultDepth = 0.18f;
        const float Gap = 0.005f;

        enum Kind { Flat, Box, Figure }

        public static List<Vector3> Place(IReadOnlyList<Vector3> sizes, float bagWidth, float bagDepth = DefaultDepth)
        {
            var spots = new List<Vector3>(sizes.Count);
            var boxes = new List<int>();
            var flats = new List<int>();
            var figures = new List<int>();
            for (int i = 0; i < sizes.Count; i++)
            {
                spots.Add(Vector3.zero);
                Kind k = KindOf(sizes[i]);
                (k == Kind.Flat ? flats : k == Kind.Box ? boxes : figures).Add(i);
            }
            boxes.Sort((a, b) => Area(sizes[b]).CompareTo(Area(sizes[a])));                        // 넓은 상자가 아래
            flats = StableSortDesc(flats, i => sizes[i].y);                                         // 팩(두꺼움) 다음 카드

            float figW = 0f, figD = 0f, figH = 0f;
            foreach (int i in figures) { figW = Mathf.Max(figW, sizes[i].x); figD = Mathf.Max(figD, sizes[i].z); figH = Mathf.Max(figH, sizes[i].y); }
            float columnW = 0f;
            foreach (int i in boxes) columnW = Mathf.Max(columnW, sizes[i].x);
            foreach (int i in flats) columnW = Mathf.Max(columnW, sizes[i].x);
            bool leftRoom = figures.Count > 0 && columnW + Gap + figW <= bagWidth;

            // ① 오른쪽 칸 (피규어가 왼쪽에 서면 오른쪽 벽에 붙임, 아니면 가운데)
            float stackX = leftRoom ? bagWidth / 2f - columnW / 2f : 0f;
            float top = 0f;
            foreach (int i in boxes) { spots[i] = new Vector3(stackX, top, 0f); top += sizes[i].y + Gap; }
            if (boxes.Count > 0) top -= Gap; // 상자 바로 위에 팩
            foreach (int i in flats) { spots[i] = new Vector3(stackX, top, 0f); top += sizes[i].y + Gap; }

            if (figures.Count == 0) return spots;
            if (!leftRoom)
            {
                foreach (int i in figures) { spots[i] = new Vector3(stackX, top, 0f); top += sizes[i].y + Gap; }
                return spots;
            }

            // ② 왼쪽 칸: 앞뒤로 나란히, 넘치면 위 층
            float figX = -(bagWidth / 2f - figW / 2f);
            int perRow = Mathf.Max(1, Mathf.FloorToInt((bagDepth + Gap) / (figD + Gap)));
            for (int n = 0; n < figures.Count; n++)
            {
                int level = n / perRow, slot = n % perRow;
                int inRow = Mathf.Min(perRow, figures.Count - level * perRow);
                float z = (slot - (inRow - 1) / 2f) * (figD + Gap);
                spots[figures[n]] = new Vector3(figX, level * (figH + Gap), z);
            }
            return spots;
        }

        public static bool IsFlat(Vector3 size) => size.y < FlatHeight;

        static Kind KindOf(Vector3 s) => IsFlat(s) ? Kind.Flat : s.y <= Mathf.Max(s.x, s.z) ? Kind.Box : Kind.Figure;

        static float Area(Vector3 size) => size.x * size.z;

        static List<int> StableSortDesc(List<int> items, System.Func<int, float> key)
        {
            var sorted = new List<int>(items);
            for (int a = 1; a < sorted.Count; a++)
            {
                int v = sorted[a], b = a - 1;
                while (b >= 0 && key(sorted[b]) < key(v)) { sorted[b + 1] = sorted[b]; b--; }
                sorted[b + 1] = v;
            }
            return sorted;
        }
    }
}
