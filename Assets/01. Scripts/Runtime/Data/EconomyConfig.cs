using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>경제 수치 (기획서 5-5).</summary>
    [CreateAssetMenu(menuName = "CardShop/Config/Economy", fileName = "EconomyConfig")]
    public class EconomyConfig : ScriptableObject
    {
        [Header("가격")]
        public int packPrice = 10;
        public int boxPrice = 90;
        public int packsPerBox = 10;
        public int freePacksPerDay = 1;

        [Header("알바비")]
        public int dailyBasePay = 40;
        public int cleanupBonus = 5;
        public int cleanupPenalty = 3;

        [Header("판매")]
        [Range(0, 1)] public float commissionRate = 0.10f;
        public int minSellPrice = 1;
        public int salesDisplaySlots = 4;

        [Header("인기 속성")]
        public bool popularElementEnabled = true;
        public float popularMultiplier = 1.5f;
        public int popularCycleDays = 2;

        [Header("시작")]
        public int startingMoney = 0;
    }
}
