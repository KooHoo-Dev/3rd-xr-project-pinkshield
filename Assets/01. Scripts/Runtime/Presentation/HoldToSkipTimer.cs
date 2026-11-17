using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// "오른손 트리거 3초 꾹 = 건너뛰기" 타이머 (시연 피드백 B3). 시간을 밖에서 받는 순수 계산이라
    /// EditMode 테스트로 고정합니다.
    /// - 누른 순간부터 시간을 잽니다(그 눌림이 카드 한 장을 넘겨도 계속 누르고 있으면 이어서 참).
    /// - 정한 시간이 처음 넘는 프레임에 한 번만 발동합니다. 다시 발동하려면 손을 뗐다가 새로 눌러야 합니다.
    /// - 잠깐 누르는 넘기기(탭)에 게이지가 깜빡이지 않도록, 유예 시간이 지나야 "충전 중"으로 봅니다.
    /// </summary>
    public sealed class HoldToSkipTimer
    {
        float pressedAt = float.NaN;
        bool spent;

        public float HoldSeconds { get; }
        public float GraceSeconds { get; }

        /// <param name="holdSeconds">꾹 눌러야 하는 시간 (초, 최소 0.1)</param>
        /// <param name="graceSeconds">이 시간보다 짧은 눌림은 게이지를 보이지 않음 (초)</param>
        public HoldToSkipTimer(float holdSeconds, float graceSeconds = 0.25f)
        {
            HoldSeconds = Mathf.Max(0.1f, holdSeconds);
            GraceSeconds = Mathf.Clamp(graceSeconds, 0f, HoldSeconds);
        }

        bool Pressing => !float.IsNaN(pressedAt);

        /// <summary>매 프레임 부릅니다. 꾹 누르기가 완성되는 프레임에만 true.</summary>
        public bool Update(bool pressed, float now)
        {
            if (!pressed)
            {
                pressedAt = float.NaN;
                spent = false; // 손을 떼면 다시 쓸 수 있음
                return false;
            }
            if (!Pressing) pressedAt = now;
            if (spent) return false;
            if (now - pressedAt >= HoldSeconds)
            {
                spent = true;
                return true;
            }
            return false;
        }

        /// <summary>게이지를 보여 줄 때인지: 누른 지 유예 시간이 지났고 아직 발동 전.</summary>
        public bool IsCharging(float now) => Pressing && !spent && now - pressedAt >= GraceSeconds;

        /// <summary>0~1 진행도. 누르지 않았거나 이미 발동했으면 0.</summary>
        public float Progress(float now) => !Pressing || spent ? 0f : Mathf.Clamp01((now - pressedAt) / HoldSeconds);

        /// <summary>
        /// 지금 누르고 있는 눌림은 무시합니다(손을 뗐다가 새로 눌러야 시작).
        /// 예: 자르기 제스처로 트리거를 누른 채 팩이 열렸을 때.
        /// </summary>
        public void RequireRelease() => spent = true;
    }
}
