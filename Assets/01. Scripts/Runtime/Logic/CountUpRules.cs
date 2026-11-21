using System;

namespace CardShop.Logic
{
    /// <summary>
    /// 숫자가 굴러 오르는 연출의 계산 (UI 개편 C안: 손목 SLIME PAY 카드의 돈 숫자).
    /// 시작 값 · 끝 값 · 진행률(0~1)을 받아 지금 보일 값을 돌려줍니다. 처음엔 빠르게, 끝에서 천천히 멈춥니다.
    /// 유니티에 기대지 않는 순수 계산이라 다른 숫자 연출에도 쓸 수 있습니다.
    /// </summary>
    public static class CountUpRules
    {
        /// <summary>굴러 오르는 데 걸리는 시간 (초)</summary>
        public const float Seconds = 0.5f;

        /// <summary>진행률 t(0~1, 범위 밖은 잘라냄)에서 보일 값. t=0이면 from, t=1이면 to</summary>
        public static int Value(int from, int to, float t)
        {
            if (!(t > 0f)) return from;   // NaN도 시작 값
            if (t >= 1f) return to;
            double inv = 1.0 - t;
            double eased = 1.0 - inv * inv * inv; // 뒤로 갈수록 느려지는 곡선
            return (int)Math.Round(from + ((double)to - from) * eased);
        }

        /// <summary>시작한 뒤 흐른 시간(초)을 진행률(0~1)로</summary>
        public static float Progress(float elapsed) => elapsed <= 0f ? 0f : Math.Min(1f, elapsed / Seconds);
    }
}
