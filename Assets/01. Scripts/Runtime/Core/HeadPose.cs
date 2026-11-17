using UnityEngine;

namespace CardShop.Core
{
    /// <summary>
    /// "머리 수평 정면에 월드 고정으로 놓기" 계산 (일시정지 메뉴 · 혼잣말 · 눈앞 팩 · 상자 카드 묶음 공용).
    /// 머리에 붙이지 않고 한 번만 놓습니다(멀미). 회전은 플레이어를 바라보는 방향(앞면 -Z가 플레이어 쪽).
    /// </summary>
    public static class HeadPose
    {
        /// <param name="distance">머리에서 수평 정면으로 (m)</param>
        /// <param name="drop">눈높이보다 이만큼 아래 (m)</param>
        public static Pose InFront(Transform head, float distance, float drop)
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up); // 바로 아래 · 위를 보고 있을 때
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            return new Pose(head.position + forward * distance + Vector3.down * drop, Quaternion.LookRotation(forward, Vector3.up));
        }
    }
}
