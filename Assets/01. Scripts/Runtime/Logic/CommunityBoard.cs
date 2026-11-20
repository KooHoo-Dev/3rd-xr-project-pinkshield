using System;
using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>카드 커뮤니티 화면에 보여줄 내용 한 묶음 (읽기 전용).</summary>
    public sealed class CommunityReport
    {
        public CardElement Today { get; }
        /// <summary>오늘 시세 전체 (1순위 · 2순위 · 강도, DEC-041)</summary>
        public MarketDay TodayMarket { get; }
        /// <summary>내일 시세 (내일이 없으면 None)</summary>
        public MarketDay TomorrowMarket { get; }
        /// <summary>내일의 인기 속성(예보). 내일이 없으면(마지막 날) None.</summary>
        public CardElement Tomorrow { get; }
        public bool HasTomorrow => Tomorrow != CardElement.None;
        /// <summary>인기 속성의 수퍼레어 이상 보유 카드, 비싼 순 (최대 CommunityBoard.TopCount장).</summary>
        public IReadOnlyList<CardData> TopCards { get; }
        public IReadOnlyList<string> MoodLines { get; }

        public CommunityReport(MarketDay today, MarketDay tomorrow, IReadOnlyList<CardData> topCards, IReadOnlyList<string> moodLines)
        {
            TodayMarket = today;
            TomorrowMarket = tomorrow;
            Today = today.Primary;
            Tomorrow = tomorrow.Primary;
            TopCards = topCards;
            MoodLines = moodLines;
        }
    }

    /// <summary>
    /// 집 컴퓨터의 카드 커뮤니티 (시연 피드백 7-3). 새 가격 규칙 없이 인기 일정 · 보유 카드 · 기존 가격 규칙만 읽습니다.
    /// </summary>
    public sealed class CommunityBoard
    {
        public const int TopCount = 3;
        public const CardRarity MinTopRarity = CardRarity.SuperRare;

        // 문구는 한 곳에 모읍니다. {0}=속성 이름, {1}=카드 이름
        public const string TodayLine = "{0} 슬라임 시세 오르는 중!";
        public const string TodayNoneLine = "오늘은 딱히 뜨는 속성이 없대요";
        public const string TomorrowLine = "내일은 {0} 슬라임이 뜬다던데? 미리 챙겨 둘 사람?";
        public const string TomorrowSameLine = "내일도 {0} 슬라임 인기 계속될 듯";
        public const string TomorrowNoneLine = "내일 시세 소식은 아직 없어요";
        public const string HaveTopLine = "{1} 갖고 있으면 지금이 기회 같은데요";
        public const string NoTopLine = "{0} 수퍼레어 이상은 다들 품절이라던데...";

        readonly MarketSchedule schedule;
        readonly CardDatabase cards;
        readonly EconomyRules economy;

        public CommunityBoard(MarketSchedule schedule, CardDatabase cards, EconomyRules economy)
        {
            this.schedule = schedule;
            this.cards = cards;
            this.economy = economy;
        }

        /// <summary>속성의 한국어 이름 (화면 · 글 공용).</summary>
        public static string ElementName(CardElement element)
        {
            switch (element)
            {
                case CardElement.Fire: return "불";
                case CardElement.Water: return "물";
                case CardElement.Earth: return "땅";
                case CardElement.Air: return "공기";
                case CardElement.Neutral: return "무속성";
                default: return "없음";
            }
        }

        public CommunityReport Build(int day, int dayCount, Collection collection)
        {
            MarketDay todayM = schedule.For(day);
            MarketDay tomorrowM = day + 1 <= dayCount ? schedule.For(day + 1) : MarketDay.None;

            List<CardData> top = FindTopCards(todayM, collection);
            return new CommunityReport(todayM, tomorrowM, top, BuildMoodLines(todayM.Primary, tomorrowM.Primary, top));
        }

        List<CardData> FindTopCards(MarketDay today, Collection collection)
        {
            var found = new List<CardData>();
            if (!today.HasPopular || collection == null) return found;

            foreach (CardData card in cards.cards)
            {
                if (card == null || card.rarity < MinTopRarity) continue;
                if (!economy.IsPopular(card, today)) continue;
                if (collection.CountOf(card.id) <= 0) continue;
                found.Add(card);
            }

            // 비싼 순, 같으면 ID 순 (결과가 항상 같도록)
            found.Sort((a, b) =>
            {
                int byPrice = economy.GrossPrice(b, today).CompareTo(economy.GrossPrice(a, today));
                return byPrice != 0 ? byPrice : string.CompareOrdinal(a.id, b.id);
            });
            if (found.Count > TopCount) found.RemoveRange(TopCount, found.Count - TopCount);
            return found;
        }

        static List<string> BuildMoodLines(CardElement today, CardElement tomorrow, List<CardData> top)
        {
            var lines = new List<string>();

            lines.Add(today == CardElement.None ? TodayNoneLine : string.Format(TodayLine, ElementName(today)));

            if (tomorrow == CardElement.None) lines.Add(TomorrowNoneLine);
            else if (tomorrow == today) lines.Add(string.Format(TomorrowSameLine, ElementName(tomorrow)));
            else lines.Add(string.Format(TomorrowLine, ElementName(tomorrow)));

            if (today != CardElement.None)
                lines.Add(top.Count > 0
                    ? string.Format(HaveTopLine, ElementName(today), top[0].displayName)
                    : string.Format(NoTopLine, ElementName(today)));

            return lines;
        }
    }
}
