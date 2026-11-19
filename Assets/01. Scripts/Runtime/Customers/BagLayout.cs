using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 봉투 안 물건 자리 정하기 (11-20 C3). 순수 계산이라 테스트(BagLayoutTests)로 확인합니다.
    /// 입력: 물건마다 크기(가로 x · 높이 y · 깊이 z, m). 출력: 물건 바닥 가운데 자리(봉투 로컬, 바닥 가운데가 원점).
    /// ① 납작한 것(높이 &lt; FlatHeight: 팩 · 카드)이 아닌 물건 중 바닥이 가장 넓은 것 = 받침(상자). 바닥 가운데에 둠
    /// ② 다른 납작하지 않은 물건(피규어)은 받침 옆 바닥에 좌우로 번갈아. 봉투 폭을 넘으면 받침 위로
    /// ③ 납작한 물건은 받침 위에 차례로 쌓음 (받침이 없으면 바닥 가운데부터)
    /// </summary>
    public static class BagLayout
    {
        public const float FlatHeight = 0.03f;
        const float Gap = 0.005f;

        public static List<Vector3> Place(IReadOnlyList<Vector3> sizes, float bagWidth)
        {
            var spots = new List<Vector3>(sizes.Count);
            for (int i = 0; i < sizes.Count; i++) spots.Add(Vector3.zero);

            int baseIndex = -1;
            for (int i = 0; i < sizes.Count; i++)
                if (!IsFlat(sizes[i]) && (baseIndex < 0 || Area(sizes[i]) > Area(sizes[baseIndex]))) baseIndex = i;

            float topOfBase = 0f;          // 받침 윗면 높이 (납작한 물건 · 넘친 피규어가 올라갈 곳)
            float left = 0f, right = 0f;   // 받침 가운데에서 왼쪽 · 오른쪽으로 차지한 폭
            if (baseIndex >= 0)
            {
                topOfBase = sizes[baseIndex].y;
                left = right = sizes[baseIndex].x / 2f;
            }

            bool nextRight = true;
            for (int i = 0; i < sizes.Count; i++)
            {
                if (i == baseIndex || IsFlat(sizes[i])) continue;
                float w = sizes[i].x;
                bool fitsRight = right + Gap + w <= bagWidth / 2f;
                bool fitsLeft = left + Gap + w <= bagWidth / 2f;
                if (nextRight && fitsRight || !nextRight && !fitsLeft && fitsRight)
                {
                    spots[i] = new Vector3(right + Gap + w / 2f, 0f, 0f);
                    right += Gap + w;
                    nextRight = false;
                }
                else if (fitsLeft)
                {
                    spots[i] = new Vector3(-(left + Gap + w / 2f), 0f, 0f);
                    left += Gap + w;
                    nextRight = true;
                }
                else
                {
                    spots[i] = new Vector3(0f, topOfBase, 0f); // 옆에 자리가 없으면 받침 위
                    topOfBase += sizes[i].y + Gap;
                }
            }

            for (int i = 0; i < sizes.Count; i++)
            {
                if (!IsFlat(sizes[i])) continue;
                spots[i] = new Vector3(0f, topOfBase, 0f);
                topOfBase += sizes[i].y + Gap;
            }
            return spots;
        }

        public static bool IsFlat(Vector3 size) => size.y < FlatHeight;
        static float Area(Vector3 size) => size.x * size.z;
    }
}
