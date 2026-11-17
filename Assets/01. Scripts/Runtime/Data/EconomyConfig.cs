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

        [Header("계산 평가 (계산시스템_확장_설계 3장 K3)")]
        [Tooltip("빨리 끝낸 계산 1건당 알바비 보너스")]
        public int checkoutBonus = 2;
        [Tooltip("기다리다 떠난 손님 1명당 감점. 0이면 감점 없음(설계서 Q2: 떠나도 감점 없음을 기본으로 유지)")]
        public int checkoutPenalty = 0;
        [Tooltip("손님이 카운터에 물건을 올린 뒤 이 시간 안에 결제하면 '빠른 계산' (물건 1개 기준, 초)")]
        public float checkoutFastSeconds = 20f;
        [Tooltip("물건이 2개째부터 한 개당 '빠른 계산' 기준에 더하는 시간 (초)")]
        public float checkoutFastPerExtraItemSeconds = 8f;

        [Header("판매")]
        [Range(0, 1)] public float commissionRate = 0.10f;
        public int minSellPrice = 1;
        public int salesDisplaySlots = 4;
        [Tooltip("가게 진열대에 올릴 수 있는 최소 등급 (이 등급 이상만 진열 가능)")]
        public CardRarity minDisplayRarity = CardRarity.SuperRare;
        [Tooltip("즉시 판매가 = 기준가 × 이 비율 (소수점 내림, 최소 1). 수수료·인기 속성 배율 없음")]
        [Range(0, 1)] public float instantSellRate = 0.6f;

        [Header("인기 속성")]
        public bool popularElementEnabled = true;
        public float popularMultiplier = 1.5f;
        public int popularCycleDays = 2;

        [Header("시작")]
        public int startingMoney = 0;
    }
}
