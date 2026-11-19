using UnityEngine;

namespace CardShop.Logic
{
    /// <summary>
    /// 바닥 얼룩 문지르기 판정 (빌드피드백_11-19_FIX설계 F5, DEC-030). 순수 계산이라 EditMode 테스트로 고정합니다.
    /// 대걸레 머리가 얼룩 위(반경 안) · 바닥 가까이 · 움직이는 중이면 움직인 수평 거리만큼 얼룩이 줄어듭니다.
    /// </summary>
    public static class StainRules
    {
        /// <summary>대걸레 머리가 이 높이(바닥 기준, m)보다 낮아야 바닥에 닿은 것으로 침</summary>
        public const float MaxHeadHeight = 0.2f;
        /// <summary>이보다 느리면(m/s) 문지르는 게 아니라 대고만 있는 것</summary>
        public const float MinScrubSpeed = 0.15f;

        /// <summary>이번 프레임에 문지른 거리 (m). 얼룩에 안 닿았거나 너무 느리면 0.</summary>
        public static float Scrubbed(Vector3 headBefore, Vector3 headNow, float dt, Vector3 stainCenter, float radius, float floorY)
        {
            if (dt <= 0f || headNow.y - floorY > MaxHeadHeight) return 0f;
            var flat = new Vector2(headNow.x - stainCenter.x, headNow.z - stainCenter.z);
            if (flat.magnitude > radius) return 0f;
            float moved = new Vector2(headNow.x - headBefore.x, headNow.z - headBefore.z).magnitude;
            return moved / dt < MinScrubSpeed ? 0f : moved;
        }

        /// <summary>남은 양 (1 = 처음, 0 = 깨끗). required = 다 지우는 데 필요한 문지른 거리 (m)</summary>
        public static float Remaining(float remaining, float scrubbed, float required) =>
            required <= 0f ? 0f : Mathf.Clamp01(remaining - scrubbed / required);
    }
}
