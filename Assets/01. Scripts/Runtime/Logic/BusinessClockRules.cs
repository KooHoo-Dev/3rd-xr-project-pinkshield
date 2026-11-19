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
    }
}
