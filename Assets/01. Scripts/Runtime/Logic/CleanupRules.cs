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
        static readonly CleanupKinds[] Order = { CleanupKinds.Trash, CleanupKinds.MessyPack };

        /// <summary>
        /// 손님 한 명이 떠날 때 만들 청소 종류 하나를 고릅니다.
        /// <paramref name="enabled"/>: 그날 켜진 종류(CampaignConfig), <paramref name="available"/>: 지금 만들 자리가 남은 종류
        /// (진열대 팩 꽂이에 팩이 남지 않았으면 흐트러진 팩은 빠짐). 둘 다 해당하는 종류 중 균등하게 뽑고, 없으면 None.
        /// </summary>
        public static CleanupKinds Pick(CleanupKinds enabled, CleanupKinds available, IRandomSource random)
        {
            CleanupKinds usable = enabled & available;
            int count = 0;
            foreach (CleanupKinds k in Order) if ((usable & k) != 0) count++;
            if (count == 0) return CleanupKinds.None;

            int pick = random.Range(0, count);
            foreach (CleanupKinds k in Order)
            {
                if ((usable & k) == 0) continue;
                if (pick-- == 0) return k;
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
