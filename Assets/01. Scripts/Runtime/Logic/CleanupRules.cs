using CardShop.Cards;
using UnityEngine;

namespace CardShop.Logic
{
    /// <summary>
    /// 청소 규칙 (기획서 5-1 · 설계서 7-4 · Q1). 화면과 상관없는 판단만 모아 EditMode 테스트로 고정합니다.
    /// 청소 구역을 실제로 만들고 치우는 일은 CleanupDirector가 합니다.
    /// </summary>
    public static class CleanupRules
    {
        static readonly CleanupKinds[] Order = { CleanupKinds.Trash, CleanupKinds.MessyPack, CleanupKinds.FloorStain };

        /// <summary>
        /// 손님이 나갈 때 청소 거리를 만들지 (F1, DEC-030). 차례대로: 이미 maxActive곳이면 안 만듦 →
        /// 1일차 첫 청소는 반드시(튜토리얼 "첫 쓰레기") → guaranteeAfter명 연속 안 생겼으면 반드시 → 그 밖엔 확률.
        /// </summary>
        /// <param name="spawnedToday">오늘 만든 청소 거리 수</param>
        /// <param name="activeCount">지금 남아 있는(안 치운) 청소 거리 수</param>
        /// <param name="quietStreak">연속으로 안 생긴 손님 수</param>
        public static bool ShouldSpawn(CampaignConfig.CleanupSpawnSettings s, int day, int spawnedToday, int activeCount, int quietStreak, IRandomSource random)
        {
            if (activeCount >= s.maxActive) return false;
            if (day <= 1 && spawnedToday == 0) return true;
            if (s.guaranteeAfter > 0 && quietStreak >= s.guaranteeAfter) return true;
            return random.NextDouble() < (day <= 1 ? s.firstDayChance : s.chance);
        }

        /// <summary>
        /// 만들 청소 종류 하나를 고릅니다 (F2: 가중치).
        /// <paramref name="enabled"/>: 그날 켜진 종류(CampaignConfig), <paramref name="available"/>: 지금 만들 자리가 남은 종류
        /// (진열대 팩 꽂이에 팩이 남지 않았으면 흐트러진 팩은 빠짐). 둘 다 해당하는 종류끼리 가중치 비율로 뽑고, 없으면 None.
        /// </summary>
        public static CleanupKinds Pick(CleanupKinds enabled, CleanupKinds available, CampaignConfig.CleanupSpawnSettings s, IRandomSource random)
        {
            CleanupKinds usable = enabled & available;
            int total = 0;
            foreach (CleanupKinds k in Order) if ((usable & k) != 0) total += s.WeightOf(k);
            if (total <= 0) return CleanupKinds.None;

            int pick = random.Range(0, total);
            foreach (CleanupKinds k in Order)
            {
                if ((usable & k) == 0) continue;
                pick -= s.WeightOf(k);
                if (pick < 0) return k;
            }
            return CleanupKinds.None;
        }

        /// <summary>
        /// 영업 마감(설계서 Q1): 손님이 모두 나간 뒤, 남은 청소가 없으면 바로, 있으면 마감 정리 시간이 지나면 정산으로 넘어갑니다.
        /// <paramref name="sinceLastCustomer"/>: 마지막 손님이 나간 뒤 흐른 시간(초).
        /// </summary>
        public static bool ClosingDone(bool hasCustomers, int pendingCleanups, float sinceLastCustomer, float closingSeconds) =>
            !hasCustomers && (pendingCleanups <= 0 || sinceLastCustomer >= closingSeconds);
    }
}
