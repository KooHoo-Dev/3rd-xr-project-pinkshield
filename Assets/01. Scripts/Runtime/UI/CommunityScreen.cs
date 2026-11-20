using System.Text;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using TMPro;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 집 컴퓨터 모니터 위의 카드 커뮤니티 화면 (시연 피드백 7-3).
    /// 집(Home) 페이즈에 들어올 때 오늘 · 내일 인기 속성, 인기 카드 TOP3, 분위기 글을 새로 채우고, 집에서만 보입니다.
    /// 내용은 순수 C# CommunityBoard가 계산하고 이 컴포넌트는 글자로 옮기기만 합니다.
    /// </summary>
    public sealed class CommunityScreen : HomePanel
    {
        // 문구는 한 곳에 모읍니다.
        // 어두운 SlimeOS 창 위 눈 편한 색 (11-20, 채도 약 70%)
        const string TodayFormat = "오늘 인기  <color=#F0A868>{0}</color> {1} · 2순위 <color=#E39A86>{2}</color>";
        const string TomorrowFormat = "내일 예보  <color=#86C3EA>{0}</color>{1} · 2순위 {2}";
        const string TomorrowNone = "내일 예보  없음 (마지막 날)";
        const string AfterTomorrowFormat = "모레 예보  <color=#B8A6F0>{0}</color>";
        const string TopHeader = "인기 카드 TOP3";
        const string TopEmpty = "아직 가진 인기 카드가 없어요";

        [SerializeField] TMP_Text forecastLabel;
        [SerializeField] TMP_Text topLabel;
        [SerializeField] TMP_Text moodLabel;

        /// <summary>
        /// 글자를 다른 곳(모니터 SlimeOS 창의 캔버스 글자)으로 옮김. 씬의 3D 글자는 숨김 (CommunityWindow, UI 개편 8번).
        /// </summary>
        public void Rehost(GameObject newContent, TMP_Text forecast, TMP_Text top, TMP_Text mood)
        {
            if (content != null) content.SetActive(false);
            content = newContent;
            forecastLabel = forecast;
            topLabel = top;
            moodLabel = mood;
            OnPhaseChanged(CurrentPhase); // 지금 집이면 바로 보이고 채움
        }

        // 집에서 카드를 팔거나 진열하면 TOP3(보유 카드 기준)가 달라지므로 HomePanel이 다시 그리게 함
        protected override void Redraw()
        {
            GameContext ctx = Context;
            if (ctx == null || ctx.Session == null || ctx.Market == null) return;

            int dayCount = DayRules.EffectiveDayCount(ctx.Campaign.DayCount, ctx.Session.Endless.Active); // 무한 모드면 늘 내일 예보 (J4)
            var board = new CommunityBoard(ctx.Market, ctx.Cards, ctx.EconomyRules);
            CommunityReport report = board.Build(ctx.Session.Day, dayCount, ctx.Session.Collection);

            if (forecastLabel != null)
            {
                bool priceListOn = ctx.Upgrades.HasPriceList(ctx.Session.Upgrades);
                bool strengthAhead = ctx.Upgrades.ForecastDays(ctx.Session.Upgrades) >= 2; // 컴퓨터 2단계: 내일 강도까지
                MarketDay tm = report.TodayMarket, nm = report.TomorrowMarket;
                string today = string.Format(TodayFormat, CommunityBoard.ElementName(tm.Primary),
                    Strength(tm, priceListOn), CommunityBoard.ElementName(tm.Secondary));
                string tomorrow = report.HasTomorrow
                    ? string.Format(TomorrowFormat, CommunityBoard.ElementName(nm.Primary),
                        strengthAhead ? " " + Strength(nm, priceListOn) : " (강도 ???)", CommunityBoard.ElementName(nm.Secondary))
                    : TomorrowNone;
                // 컴퓨터 업그레이드 2단계: 모레 인기 속성까지 (집_업그레이드_설계 3장)
                CardElement after = ctx.Upgrades.ForecastDays(ctx.Session.Upgrades) >= 2
                    ? ctx.Market.For(ctx.Session.Day + 2).Primary : CardElement.None;
                if (after != CardElement.None) tomorrow += "\n" + string.Format(AfterTomorrowFormat, CommunityBoard.ElementName(after));
                forecastLabel.text = today + "\n" + tomorrow;
            }

            if (topLabel != null)
            {
                bool priceList = ctx.Upgrades.HasPriceList(ctx.Session.Upgrades);
                var sb = new StringBuilder(priceList ? TopHeader + " · 시세표" : TopHeader);
                if (report.TopCards.Count == 0)
                {
                    sb.Append('\n').Append(TopEmpty);
                }
                else
                {
                    for (int i = 0; i < report.TopCards.Count; i++)
                    {
                        CardData card = report.TopCards[i];
                        int price = ctx.EconomyRules.GrossPrice(card, report.TodayMarket);
                        sb.Append('\n').Append(i + 1).Append(". ").Append(card.displayName).Append("  ").Append(MoneyText.Format(price));
                        // 컴퓨터 업그레이드 3단계: 카드 시세표 (오늘 · 내일 값)
                        if (priceList && report.HasTomorrow)
                            sb.Append(" → 내일 ").Append(MoneyText.Format(ctx.EconomyRules.GrossPrice(card, report.TomorrowMarket)));
                    }
                }
                topLabel.text = sb.ToString();
            }

            if (moodLabel != null)
                moodLabel.text = string.Join("\n", report.MoodLines);
        }

        // 강도 이름 (시세표가 있으면 정확한 배율까지): "(급등)" / "(급등 ×2.0)"
        static string Strength(MarketDay m, bool exact) =>
            !m.HasPopular ? "" : exact ? $"({m.TierName} ×{m.PrimaryMultiplier:0.0#})" : $"({m.TierName})";
    }
}
