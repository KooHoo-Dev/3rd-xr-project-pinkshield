using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 자막 자리 계산 (11-20 피드백 "자막이 가끔 안 보임"). 퇴근 · 아침 혼잣말(DayTransition → WorldToast)처럼
    /// 새 안내가 뜰 때 보는 방향 정면에 한 번 놓고 월드에 고정합니다 (둘러볼 때 따라다니지 않음, 11-20 H2).
    /// 혼잣말과 달리 자막은 할 일을 마칠 때까지 오래 떠 있으므로, 시야에서 벗어난 채 잠시 지나거나 멀리 걸어가면 다시 정면에 놓습니다.
    /// 예전 방식(지평선 기준 32° 아래)은 위를 보면 시야 아래로 빠졌음. 순수 계산이라 EditMode 테스트로 확인합니다.
    /// </summary>
    public static class CaptionPlacement
    {
        /// <summary>눈에서 (m)</summary>
        public const float Distance = 1.0f;
        /// <summary>수평 정면보다 아래로 (도). 혼잣말(약 3°)보다 조금 낮게 — 손 · 물건을 덜 가리게 (11-19 2차)</summary>
        public const float PitchDown = 10f;
        /// <summary>보는 방향에서 이만큼 넘게 벗어나면 "시야 밖" (도). Quest 시야 절반(약 45°)보다 조금 넓게</summary>
        public const float OutOfViewAngle = 50f;
        /// <summary>시야 밖이 이만큼 이어지면 다시 놓음 (초). 잠깐 둘러보는 것으로는 움직이지 않게</summary>
        public const float OutOfViewSeconds = 1.0f;
        /// <summary>놓은 자리에서 머리가 이만큼 넘게 (수평으로) 걸어가면 다시 놓음 (m)</summary>
        public const float WalkAwayDistance = 1.5f;

        /// <summary>위를 볼 때 따라 올라가는 한계 (도, 수평 위)</summary>
        public const float MaxPitchUp = 40f;

        /// <summary>
        /// 보는 방향 정면에 놓을 자리. 앞면(-Z)이 플레이어 쪽.
        /// 높이: 수평 · 아래를 볼 때는 수평에서 PitchDown 아래 (계산대에서 손을 내려다볼 때 손을 가리지 않게),
        /// 위를 볼 때만 시선보다 PitchDown 아래로 따라 올라감 (진열대 · 벽시계를 볼 때 시야 아래로 빠지지 않게).
        /// </summary>
        public static Pose Front(Vector3 headPosition, Vector3 headForward)
        {
            Vector3 flat = Vector3.ProjectOnPlane(headForward, Vector3.up);
            if (flat.sqrMagnitude < 0.001f) flat = Vector3.forward; // 바로 위 · 아래를 볼 때
            float yaw = Quaternion.LookRotation(flat.normalized, Vector3.up).eulerAngles.y;
            float pitch = PitchFor(GazeDownAngle(headForward));
            Vector3 dir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
            return new Pose(headPosition + dir * Distance, Quaternion.LookRotation(dir, Vector3.up));
        }

        /// <summary>시선이 수평보다 아래로 몇 도인가 (위를 보면 음수)</summary>
        public static float GazeDownAngle(Vector3 headForward) =>
            -Mathf.Asin(Mathf.Clamp(headForward.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;

        /// <summary>시선 각도(아래 +) → 자막 각도(아래 +). 순수, 테스트</summary>
        public static float PitchFor(float gazeDown) => Mathf.Clamp(gazeDown + PitchDown, -MaxPitchUp, PitchDown);

        /// <summary>자막이 지금 보는 방향에서 OutOfViewAngle 넘게 벗어났는가</summary>
        public static bool IsOutOfView(Vector3 headPosition, Vector3 headForward, Vector3 captionPosition)
        {
            Vector3 to = captionPosition - headPosition;
            if (to.sqrMagnitude < 0.0001f) return false;
            return Vector3.Angle(headForward, to) > OutOfViewAngle;
        }

        /// <summary>놓은 자리(그때 머리 위치)에서 수평으로 WalkAwayDistance 넘게 걸어갔는가</summary>
        public static bool WalkedAway(Vector3 placedFromHead, Vector3 headPosition)
        {
            Vector3 d = headPosition - placedFromHead;
            d.y = 0f;
            return d.magnitude > WalkAwayDistance;
        }
    }
}
