using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>
    /// 일차별 인기 속성 (기획서 5-4: 2일마다 속성 1개). 게임 시작 때 전체 일정을 미리 뽑아 둡니다
    /// (같은 시드면 같은 일정 → 재현·디버그가 쉬움). 바로 앞 주기와 같은 속성은 다시 뽑지 않습니다.
    /// </summary>
    public sealed class PopularElementSchedule
    {
        static readonly CardElement[] SlimeElements =
            { CardElement.Fire, CardElement.Water, CardElement.Earth, CardElement.Air, CardElement.Neutral };

        readonly CardElement[] byDay;

        public PopularElementSchedule(int dayCount, int cycleDays, IRandomSource random)
        {
            byDay = new CardElement[dayCount];
            if (cycleDays < 1) cycleDays = 1;
            CardElement current = CardElement.None;
            for (int d = 0; d < dayCount; d++)
            {
                if (d % cycleDays == 0)
                {
                    CardElement next;
                    do next = SlimeElements[random.Range(0, SlimeElements.Length)];
                    while (next == current);
                    current = next;
                }
                byDay[d] = current;
            }
        }

        /// <summary>1부터 시작하는 일차의 인기 속성. 범위 밖이면 None.</summary>
        public CardElement For(int day) => day >= 1 && day <= byDay.Length ? byDay[day - 1] : CardElement.None;
    }
}
