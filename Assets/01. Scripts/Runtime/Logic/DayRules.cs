namespace CardShop.Logic
{
    /// <summary>
    /// 일차 판단을 한 곳에 (무한모드_설계 4장): 마지막 날인가 · 일차 글자 · 연장 날 손님 늘리기.
    /// 흩어져 있던 "day >= DayCount" 비교가 무한 모드에서 매일 마지막 날이 되던 문제를 막습니다. 순수 C#, DayRulesTests.
    /// </summary>
    public static class DayRules
    {
        /// <summary>마지막 날인가 (집을 건너뛰고 최종 결과로). 무한 모드면 늘 false</summary>
        public static bool IsLastDay(int day, int dayCount, bool endless) => !endless && day >= dayCount;

        /// <summary>"내일"이 있는 날인가 (커뮤니티 예보 · 아침 말이 쓰는 남은 날 수 기준). 무한 모드면 끝이 없음</summary>
        public static int EffectiveDayCount(int dayCount, bool endless) => endless ? int.MaxValue : dayCount;

        /// <summary>달력 · 정산 아래 줄: "/ 7일" 또는 "연장 2일"</summary>
        public static string DaySuffix(int day, int dayCount) =>
            day > dayCount ? $"연장 {day - dayCount}일" : $"/ {dayCount}일";

        /// <summary>연장 날의 손님 추가 수 (Q3: 사흘마다 +1명, 최대 max). 공식 날에는 0</summary>
        public static int ExtraCustomers(int day, int dayCount, int everyDays, int max)
        {
            if (day <= dayCount || everyDays < 1) return 0;
            int extra = (day - dayCount - 1) / everyDays + 1;
            return extra > max ? max : extra;
        }
    }
}
