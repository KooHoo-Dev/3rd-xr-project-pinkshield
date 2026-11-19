using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>
    /// 가게(영업 · 퇴근 전) 배경 음악 고르기 (11-19 사운드 연결).
    /// 1일차는 BgmShop 고정, 2일차부터는 BgmShop · BgmShopChill 중 그날 정한 곡.
    /// 전날 유니크 이상을 뽑았으면 다음 날은 BgmShopLucky — 기분 좋은 날을 표현 (아침 혼잣말 MorningLines의 Unique 기분과 같은 기준).
    /// </summary>
    public static class ShopBgm
    {
        /// <param name="bestRarityYesterday">어제 뽑은 가장 높은 등급 (GameSession.Previous.bestRarity, 1일차 · 안 열었으면 -1)</param>
        /// <param name="chillToday">2일차부터: 오늘은 BgmShopChill인가 (하루에 한 번 정함)</param>
        public static SoundId Pick(int day, int bestRarityYesterday, bool chillToday)
        {
            if (day <= 1) return SoundId.BgmShop;
            if (bestRarityYesterday >= (int)CardRarity.Unique) return SoundId.BgmShopLucky;
            return chillToday ? SoundId.BgmShopChill : SoundId.BgmShop;
        }
    }
}
