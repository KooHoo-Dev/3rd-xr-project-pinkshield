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
        // 즉시 판매가는 등급별 매입가(RarityTable.buybackPrice)로 바뀜 (경제시스템_설계 5장, DEC-041)

        [Header("손님이 진열 카드 고르기 (경제시스템_설계 13-2 · 8장)")]
        [Tooltip("손님은 진열 카드를 모두 둘러보고, 카드마다 '등급 구매 확률 × 이 값'으로 판정해 처음 통과한 1장을 삽니다")]
        [Range(0, 1)] public float browseBuyFactor = 0.4f;
        [Tooltip("둘러보는 순서 가중치: 1순위 · 2순위 인기 카드를 먼저 볼 가능성")]
        public float primaryLookWeight = 3f;
        public float secondaryLookWeight = 2f;
        [Tooltip("인기 카드 구매 확률 배율")]
        public float primaryBuyBoost = 1.3f;
        public float secondaryBuyBoost = 1.15f;
        [Range(0, 1)] public float maxBuyChance = 0.95f;

        [Header("인기 속성 (경제시스템_설계 6 · 7장: 1순위 · 2순위, 강도 등급)")]
        public bool popularElementEnabled = true;
        [Tooltip("공식 기간(7일)의 인기 주기 (일)")]
        public int popularCycleDays = 2;
        [Tooltip("1순위 강도 등급: 주기마다 weight 비율로 하나를 뽑음")]
        public PopularTier[] popularTiers =
        {
            new PopularTier { name = "소폭 상승", multiplier = 1.3f, weight = 30f },
            new PopularTier { name = "상승", multiplier = 1.6f, weight = 40f },
            new PopularTier { name = "급등", multiplier = 2.0f, weight = 22f },
            new PopularTier { name = "폭등", multiplier = 3.0f, weight = 8f },
        };
        [Tooltip("2순위 배율 = 1 + (1순위 배율 − 1) × 이 값")]
        [Range(0, 1)] public float secondaryBonusShare = 0.5f;
        [Tooltip("무한 모드 연장 날의 주기 길이 (최소 · 최대 일)")]
        public Vector2Int endlessCycleDays = new Vector2Int(1, 3);
        [Tooltip("무한 모드: 연장 3일마다 '소폭 상승' 가중치를 이만큼 마지막 강도(폭등)로 옮김")]
        public float endlessBoomShiftPer3Days = 2f;
        public float endlessBoomShiftMax = 12f;
        [Tooltip("무한 모드: 연장 이 날부터 '역대급' 강도와 하락 속성이 나옴 (경제시스템_설계 7-1)")]
        public int endlessWildFromExtraDay = 4;
        public PopularTier endlessWildTier = new PopularTier { name = "역대급", multiplier = 4f, weight = 2f };
        [Tooltip("하락 속성 배율 (인기 2개와 다른 속성 1개)")]
        public float fallingMultiplier = 0.8f;
        [Tooltip("손님이 하락 속성 카드를 먼저 볼 가중치")]
        public float fallingLookWeight = 0.5f;

        [System.Serializable]
        public class PopularTier
        {
            [Tooltip("커뮤니티 · 혼잣말에 보이는 강도 이름")]
            public string name;
            public float multiplier = 1.5f;
            public float weight = 1f;
        }

        [Header("시작")]
        public int startingMoney = 0;
    }
}
