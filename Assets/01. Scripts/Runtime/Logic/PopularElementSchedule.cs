using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>
    /// 일차별 인기 속성 (기획서 5-4: 2일마다 속성 1개). 게임 시작 때 전체 일정을 미리 뽑아 둡니다
    /// (같은 시드면 같은 일정 → 재현·디버그가 쉬움). 바로 앞 주기와 같은 속성은 다시 뽑지 않습니다.
    /// 무한 모드로 날이 늘면 같은 규칙으로 뒤를 이어 뽑습니다.
    /// </summary>
    public sealed class PopularElementSchedule
    {
        static readonly CardElement[] SlimeElements =
            { CardElement.Fire, CardElement.Water, CardElement.Earth, CardElement.Air, CardElement.Neutral };

        readonly System.Collections.Generic.List<CardElement> byDay = new System.Collections.Generic.List<CardElement>();
        readonly int cycleDays;
        readonly IRandomSource random;
        CardElement current = CardElement.None;

        public PopularElementSchedule(int dayCount, int cycleDays, IRandomSource random)
        {
            this.cycleDays = cycleDays < 1 ? 1 : cycleDays;
            this.random = random;
            Extend(dayCount);
        }

        // d일째까지 일정을 채움 (무한 모드에서 8일차 이후도 같은 규칙으로 이어 뽑음, 11-20 J4)
        void Extend(int days)
        {
            while (byDay.Count < days)
            {
                if (byDay.Count % cycleDays == 0)
                {
                    CardElement next;
                    do next = SlimeElements[random.Range(0, SlimeElements.Length)];
                    while (next == current);
                    current = next;
                }
                byDay.Add(current);
            }
        }

        /// <summary>1부터 시작하는 일차의 인기 속성. 0 이하는 None, 처음 일정보다 뒤면 이어서 뽑음.</summary>
        public CardElement For(int day)
        {
            if (day < 1 || day > 10000) return CardElement.None;
            Extend(day);
            return byDay[day - 1];
        }
    }
}
