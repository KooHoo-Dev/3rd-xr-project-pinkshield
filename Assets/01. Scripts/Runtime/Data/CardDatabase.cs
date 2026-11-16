using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>전체 카드 목록. CSV 임포터가 자동으로 채웁니다.</summary>
    [CreateAssetMenu(menuName = "CardShop/Card Database", fileName = "CardDatabase")]
    public class CardDatabase : ScriptableObject
    {
        public List<CardData> cards = new List<CardData>();

        Dictionary<string, CardData> byId;
        List<CardData>[] byRarity;

        public int Count => cards.Count;

        public CardData Get(string id)
        {
            BuildCache();
            return id != null && byId.TryGetValue(id, out CardData c) ? c : null;
        }

        public IReadOnlyList<CardData> GetByRarity(CardRarity rarity)
        {
            BuildCache();
            return byRarity[(int)rarity];
        }

        /// <summary>목록을 바꾼 뒤(에디터 임포트 등) 호출하세요.</summary>
        public void InvalidateCache()
        {
            byId = null;
            byRarity = null;
        }

        void OnEnable() => InvalidateCache();

        void BuildCache()
        {
            if (byId != null) return;
            byId = new Dictionary<string, CardData>();
            byRarity = new List<CardData>[CardEnumUtil.RarityCount];
            for (int i = 0; i < byRarity.Length; i++) byRarity[i] = new List<CardData>();

            foreach (CardData c in cards)
            {
                if (c == null || string.IsNullOrEmpty(c.id)) continue;
                byId[c.id] = c;
                byRarity[(int)c.rarity].Add(c);
            }
        }
    }
}
