using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>바인더 위쪽 속성 탭. 값은 화면의 탭 순서입니다 (BinderTabs.Order).</summary>
    public enum BinderTab
    {
        All = 0,     // 전체 (도감 번호순)
        Fire = 1,
        Water = 2,
        Earth = 3,
        Air = 4,
        Neutral = 5,
        Other = 6,   // 필드 · 슬라임 알 (속성 없음)
    }

    /// <summary>
    /// 바인더 속성별 보기 규칙 (순수 C#, 2차 기기 테스트 피드백).
    /// 커뮤니티에서 본 인기 속성 카드를 바인더에서 바로 찾을 수 있게, 탭 하나 = 그 속성 카드만 모아 보기.
    /// 고른 카드들의 순서는 도감 순서(CardDatabase) 그대로입니다.
    /// </summary>
    public static class BinderTabs
    {
        /// <summary>화면 왼쪽부터의 탭 순서 (씬 빌더의 버튼 순서와 같음).</summary>
        public static readonly BinderTab[] Order =
        {
            BinderTab.All, BinderTab.Fire, BinderTab.Water, BinderTab.Earth, BinderTab.Air, BinderTab.Neutral, BinderTab.Other,
        };

        public static string Label(BinderTab tab)
        {
            switch (tab)
            {
                case BinderTab.All: return "전체";
                case BinderTab.Other: return "기타";
                default: return CommunityBoard.ElementName(ToElement(tab));
            }
        }

        /// <summary>그 속성의 카드가 들어가는 탭 (전체 탭 제외). 속성 없는 카드는 기타.</summary>
        public static BinderTab ForElement(CardElement element)
        {
            switch (element)
            {
                case CardElement.Fire: return BinderTab.Fire;
                case CardElement.Water: return BinderTab.Water;
                case CardElement.Earth: return BinderTab.Earth;
                case CardElement.Air: return BinderTab.Air;
                case CardElement.Neutral: return BinderTab.Neutral;
                default: return BinderTab.Other;
            }
        }

        /// <summary>탭이 가리키는 속성. 전체 · 기타 탭은 None.</summary>
        public static CardElement ToElement(BinderTab tab)
        {
            switch (tab)
            {
                case BinderTab.Fire: return CardElement.Fire;
                case BinderTab.Water: return CardElement.Water;
                case BinderTab.Earth: return CardElement.Earth;
                case BinderTab.Air: return CardElement.Air;
                case BinderTab.Neutral: return CardElement.Neutral;
                default: return CardElement.None;
            }
        }

        public static bool Contains(BinderTab tab, CardElement element) => tab == BinderTab.All || ForElement(element) == tab;

        /// <summary>탭에 보일 카드 ID (도감 순서 유지). 비어 있는(null) 카드는 뺍니다.</summary>
        public static List<string> FilterIds(IEnumerable<CardData> cardsInOrder, BinderTab tab)
        {
            var ids = new List<string>();
            if (cardsInOrder == null) return ids;
            foreach (CardData c in cardsInOrder)
                if (c != null && Contains(tab, c.element)) ids.Add(c.id);
            return ids;
        }
    }
}
