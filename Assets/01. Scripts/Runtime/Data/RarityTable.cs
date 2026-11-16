using System;
using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>
    /// 등급별 경제·연출 값 (기획서 5-4 가격표). 같은 등급의 카드는 값이 같으므로 카드가 아니라 등급에 둡니다.
    /// 외형(셰이더 수치)은 CardRarityStyles에 있습니다.
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Config/Rarity Table", fileName = "RarityTable")]
    public class RarityTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public int basePrice;
            [Range(0, 1)] public float customerBuyChance;
            [Tooltip("개봉 시 새어 나오는 빛 등 연출 색")]
            public Color revealColor = Color.white;
            [Tooltip("상자 개봉 때 하이라이트 연출")]
            public bool highlightInBox;
        }

        [Tooltip("CardRarity 순서: 노말, 레어, 수퍼레어, 유니크, 레전더리")]
        public Entry[] entries =
        {
            new Entry { basePrice = 1,   customerBuyChance = 0.80f, revealColor = new Color(0.85f, 0.85f, 0.85f), highlightInBox = false },
            new Entry { basePrice = 5,   customerBuyChance = 0.60f, revealColor = new Color(0.35f, 0.65f, 1.00f), highlightInBox = true },
            new Entry { basePrice = 20,  customerBuyChance = 0.35f, revealColor = new Color(0.70f, 0.40f, 1.00f), highlightInBox = true },
            new Entry { basePrice = 100, customerBuyChance = 0.12f, revealColor = new Color(1.00f, 0.45f, 0.75f), highlightInBox = true },
            new Entry { basePrice = 500, customerBuyChance = 0.03f, revealColor = new Color(1.00f, 0.82f, 0.25f), highlightInBox = true },
        };

        public Entry Get(CardRarity rarity) => entries[(int)rarity];
        public int BasePrice(CardRarity rarity) => Get(rarity).basePrice;
    }
}
