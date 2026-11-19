namespace CardShop.Logic
{
    /// <summary>영업 시계 규칙 (FIX D6 1일차 빨리 끝내기). 순수 판단만, BusinessClockRulesTests.</summary>
    public static class BusinessClockRules
    {
        /// <summary>
        /// 영업 시계가 흐르는 배율. 빨리 감기가 켜진 날, 손님이 나오는 구간(앞쪽 spawnWindowRatio)이 지났고
        /// 손님(도둑 포함)이 하나도 없고 남은 청소가 없으면 fastForward배, 아니면 1.
        /// </summary>
        /// <param name="elapsedRatio">영업 시간 중 지난 비율 (0~1)</param>
        public static float Speed(bool fastForwardDay, float elapsedRatio, float spawnWindowRatio, bool hasCustomers, int pendingCleanup, float fastForward)
        {
            bool idle = fastForwardDay && elapsedRatio >= spawnWindowRatio && !hasCustomers && pendingCleanup == 0;
            return idle && fastForward > 1f ? fastForward : 1f;
        }

        // ---- 가게 시계 (11-20 F6): 영업 시간을 9시 → 16시로, 마감 청소는 16시부터 1초 = 1분
        public const float OpenHour = 9f;
        public const float CloseHour = 16f;

        /// <summary>영업 진행 비율(0~1) → 시각(시, 소수)</summary>
        public static float HourAt(float progress) => OpenHour + (CloseHour - OpenHour) * (progress < 0f ? 0f : progress > 1f ? 1f : progress);

        /// <summary>마감 뒤 지난 초 → 시각 (1초 = 1분)</summary>
        public static float HourAfterClose(float secondsSinceClose) => CloseHour + (secondsSinceClose < 0f ? 0f : secondsSinceClose) / 60f;

        /// <summary>시각 → "09:05" (분은 내림)</summary>
        public static string Format(float hour)
        {
            int total = (int)System.Math.Floor(hour * 60f + 0.0001f);
            return $"{total / 60 % 24:00}:{total % 60:00}";
        }
    }
}
