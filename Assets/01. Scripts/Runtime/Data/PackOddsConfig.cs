using System;
using UnityEngine;

namespace CardShop.Cards
{
    public enum PackSlotType
    {
        Common = 0, // 1~3번 일반 슬롯
        Rare = 1,   // 4번 레어 슬롯
        Hit = 2,    // 5번 히트 슬롯
    }

    /// <summary>등급별 확률(%) 5개. 순서는 CardRarity와 같습니다.</summary>
    [Serializable]
    public struct RarityWeights
    {
        public float normal, rare, superRare, unique, legendary;

        public RarityWeights(float n, float r, float sr, float u, float l)
        {
            normal = n; rare = r; superRare = sr; unique = u; legendary = l;
        }

        public float this[CardRarity rarity]
        {
            get
            {
                switch (rarity)
                {
                    case CardRarity.Normal: return normal;
                    case CardRarity.Rare: return rare;
                    case CardRarity.SuperRare: return superRare;
                    case CardRarity.Unique: return unique;
                    default: return legendary;
                }
            }
            set
            {
                switch (rarity)
                {
                    case CardRarity.Normal: normal = value; break;
                    case CardRarity.Rare: rare = value; break;
                    case CardRarity.SuperRare: superRare = value; break;
                    case CardRarity.Unique: unique = value; break;
                    default: legendary = value; break;
                }
            }
        }

        public float Sum => normal + rare + superRare + unique + legendary;
    }

    /// <summary>
    /// 팩 확률 설정 (기획서 5-3). 단서 보정 확률은 표로 저장하지 않고 <see cref="GetSlotWeights"/>에서 계산합니다.
    /// 기본 확률만 고치면 단서 확률이 따라 바뀝니다.
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Config/Pack Odds", fileName = "PackOddsConfig")]
    public class PackOddsConfig : ScriptableObject
    {
        [Header("슬롯 구성 (팩 1개 = 이 배열 길이만큼의 카드)")]
        public PackSlotType[] slotLayout =
        {
            PackSlotType.Common, PackSlotType.Common, PackSlotType.Common, PackSlotType.Rare, PackSlotType.Hit,
        };

        [Header("슬롯별 기본 확률 (%)")]
        public RarityWeights commonSlot = new RarityWeights(93f, 6.6f, 0.4f, 0f, 0f);
        public RarityWeights rareSlot = new RarityWeights(30f, 58f, 10.7f, 1.2f, 0.1f);
        public RarityWeights hitSlot = new RarityWeights(0f, 66f, 25f, 8.4f, 0.6f);

        [Header("단서 보정 (단일 팩)")]
        [Tooltip("단서 개수(0, 1, 2)별 배율")]
        public float[] clueMultipliers = { 1f, 1.5f, 2f };
        [Tooltip("이 등급 이상에 배율을 곱합니다")]
        public CardRarity clueBoostFrom = CardRarity.SuperRare;
        [Tooltip("배율을 적용할 슬롯 종류")]
        public PackSlotType[] clueBoostedSlots = { PackSlotType.Rare, PackSlotType.Hit };

        [Header("특수 팩 (팩 1개당, 단서와 무관)")]
        [Tooltip("갓팩 확률 (%)")] public float godPackChance = 0.2f;
        [Tooltip("갓팩 카드 1장이 레전더리일 확률 (%), 나머지는 유니크")] public float godPackLegendaryShare = 15f;
        [Tooltip("레전더리 확정 팩 확률 (%)")] public float legendaryPackChance = 1.0f;

        [Header("선반 단서 배치 비율 (%)")]
        public float shelfNoClue = 75f;
        public float shelfOneClue = 20f;
        public float shelfTwoClues = 5f;

        public int CardsPerPack => slotLayout.Length;

        public RarityWeights GetBaseWeights(PackSlotType slot)
        {
            switch (slot)
            {
                case PackSlotType.Common: return commonSlot;
                case PackSlotType.Rare: return rareSlot;
                default: return hitSlot;
            }
        }

        /// <summary>
        /// 단서 개수를 반영한 슬롯 확률(%).
        /// 규칙: clueBoostFrom 이상 등급 × 배율, 남는 확률을 그 아래 등급들에 기존 비율대로 재분배.
        /// </summary>
        public RarityWeights GetSlotWeights(PackSlotType slot, int clueCount)
        {
            RarityWeights w = GetBaseWeights(slot);
            if (clueCount <= 0 || Array.IndexOf(clueBoostedSlots, slot) < 0) return w;

            float mult = clueMultipliers[Mathf.Clamp(clueCount, 0, clueMultipliers.Length - 1)];
            float total = w.Sum;
            float boosted = 0f, lowerOriginal = 0f;

            for (int r = 0; r < CardEnumUtil.RarityCount; r++)
            {
                var rarity = (CardRarity)r;
                if (rarity >= clueBoostFrom) { w[rarity] *= mult; boosted += w[rarity]; }
                else lowerOriginal += w[rarity];
            }

            float remaining = Mathf.Max(0f, total - boosted);
            for (int r = 0; r < (int)clueBoostFrom; r++)
            {
                var rarity = (CardRarity)r;
                w[rarity] = lowerOriginal > 0f ? w[rarity] / lowerOriginal * remaining : 0f;
            }
            return w;
        }
    }
}
