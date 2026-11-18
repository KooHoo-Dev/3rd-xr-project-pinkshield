using System;
using System.Collections.Generic;
using CardShop.Customers;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>
    /// 손님이 계산대에 가져오는 가게 재고 품목과 장바구니 개수 (계산시스템_확장_설계 5-1 · 5-3).
    /// 판매 등록 카드는 여기 없고 SalesRules가 정합니다(손님당 최대 1장).
    /// 프리팹이 비어 있는 품목은 팔리지 않습니다(물건 없이 줄만 생기는 일 방지).
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Config/Shop Items", fileName = "ShopItemConfig")]
    public class ShopItemConfig : ScriptableObject
    {
        [Serializable]
        public class Item
        {
            public string displayName = "카드팩";
            [Tooltip("손님이 내는 값. 카드팩은 EconomyConfig.packPrice와 맞춰 둠")]
            public int price = 10;
            [Tooltip("뽑힐 가중치 (0이면 안 팔림)")]
            public int weight = 1;
            [Tooltip("이 일차부터 팔림 (1부터)")]
            public int firstDay = 1;
            [Tooltip("손님 한 명이 살 수 있는 최대 개수. 0 = 제한 없음 (카드팩). 피규어 · 상자는 1")]
            public int maxPerCustomer = 0;
            [Tooltip("계산대에 올라오는 물건 (ScannableItem). CustomerFloorBuilder가 카드팩은 자동으로 채움")]
            public ScannableItem prefab;
            [Tooltip("계산대에 올라올 때 크기 배율 (11-19 피드백: 카드팩이 작아 보임 → 1.2)")]
            public float counterScale = 1f;
        }

        public List<Item> items = new List<Item>
        {
            new Item { displayName = "카드팩", price = 10, weight = 6, firstDay = 1 },
        };

        [Header("장바구니 개수")]
        [Tooltip("0번 = 1개, 1번 = 2개 … 의 가중치")]
        public List<int> countWeights = new List<int> { 50, 30, 15, 5 };
        [Tooltip("일차별 최대 개수 (0번 = 1일차). 일차가 더 많으면 마지막 값")]
        public List<int> maxCountByDay = new List<int> { 2, 2, 3, 3, 4, 4, 4 };

        public int MaxCount(int day)
        {
            if (maxCountByDay == null || maxCountByDay.Count == 0) return 1;
            return maxCountByDay[Mathf.Clamp(day - 1, 0, maxCountByDay.Count - 1)];
        }

        /// <summary>프리팹이 있는 품목만 로직용으로 넘깁니다.</summary>
        public List<StockOption> Options()
        {
            var list = new List<StockOption>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                if (it == null || it.prefab == null) continue;
                list.Add(new StockOption(i, it.displayName, it.price, it.weight, it.firstDay, it.maxPerCustomer));
            }
            return list;
        }
    }
}
