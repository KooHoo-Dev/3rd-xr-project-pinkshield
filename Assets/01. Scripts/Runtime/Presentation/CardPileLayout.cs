using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 넘긴 카드가 카운터 한 곳에 차곡차곡 쌓이는 자리 계산 (시연 피드백 B1·B2).
    /// 순수 계산이라 EditMode 테스트로 고정합니다 (CardPileLayoutTests).
    /// - 높이: 카드 한 장 두께만큼씩 위로
    /// - 비뚤어짐: 장마다 조금씩 다른 각도·위치. 같은 순번이면 항상 같은 값(난수 없음)이라 다시 쌓아도 모양이 같습니다.
    /// </summary>
    public static class CardPileLayout
    {
        /// <summary>
        /// 카드를 앞면이 위로 오게 눕히는 기준 방향. 카드 앞면 = 로컬 -Z, 카드 윗변(+Y)은 플레이어 반대쪽.
        /// </summary>
        /// <param name="awayFromPlayer">플레이어 반대쪽 방향(수평이 아니어도 수평으로 맞춤)</param>
        public static Quaternion FlatRotation(Vector3 awayFromPlayer)
        {
            Vector3 flat = Vector3.ProjectOnPlane(awayFromPlayer, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            return Quaternion.LookRotation(Vector3.down, flat.normalized); // 로컬 +Z가 아래 → 앞면(-Z)이 위
        }

        /// <summary>index번째 카드(0 = 맨 아래)가 바닥에서 떠 있는 높이.</summary>
        public static float Height(int index, float thickness) => Mathf.Max(0, index) * thickness;

        /// <summary>
        /// 순번마다 정해진 -1~1 사이 값. 황금비 수열이라 이웃한 카드끼리 값이 고르게 흩어집니다.
        /// </summary>
        public static float Jitter(int index, float phase)
        {
            float v = index * 0.618034f + phase;
            v -= Mathf.Floor(v);
            return v * 2f - 1f;
        }

        /// <param name="basePose">더미 맨 아래 카드 자리. 회전은 <see cref="FlatRotation"/>으로 만든 값</param>
        /// <param name="maxAngle">수직축 기준 최대 비뚤어짐(도)</param>
        /// <param name="maxOffset">수평 최대 어긋남(m)</param>
        public static Pose GetPose(int index, Pose basePose, float thickness, float maxAngle, float maxOffset)
        {
            float angle = Jitter(index, 0.13f) * maxAngle;
            // 카드 로컬 X(좌우)·Y(앞뒤)가 눕힌 상태에서 수평면이므로 그 평면 안에서만 어긋나게 합니다.
            var offset = new Vector3(Jitter(index, 0.47f), Jitter(index, 0.81f), 0f) * maxOffset;
            Vector3 position = basePose.position + basePose.rotation * offset + Vector3.up * Height(index, thickness);
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up) * basePose.rotation;
            return new Pose(position, rotation);
        }
    }
}
