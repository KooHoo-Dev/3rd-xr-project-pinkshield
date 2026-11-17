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
        const string TodayFormat = "오늘 인기  <color=#FFC857>{0}</color>";
        const string TomorrowFormat = "내일 예보  <color=#7FD1FF>{0}</color>";
        const string TomorrowNone = "내일 예보  없음 (마지막 날)";
        const string TopHeader = "인기 카드 TOP3";
        const string TopEmpty = "아직 가진 인기 카드가 없어요";

        [SerializeField] TMP_Text forecastLabel;
        [SerializeField] TMP_Text topLabel;
        [SerializeField] TMP_Text moodLabel;

        // 집에서 카드를 팔거나 진열하면 TOP3(보유 카드 기준)가 달라지므로 HomePanel이 다시 그리게 함
        protected override void Redraw()
        {
            GameContext ctx = Context;
            if (ctx == null || ctx.Session == null || ctx.PopularElements == null) return;

            int dayCount = ctx.Campaign.DayCount;
            var board = new CommunityBoard(ctx.PopularElements, ctx.Cards, ctx.EconomyRules);
            CommunityReport report = board.Build(ctx.Session.Day, dayCount, ctx.Session.Collection);

            if (forecastLabel != null)
            {
                string today = string.Format(TodayFormat, CommunityBoard.ElementName(report.Today));
                string tomorrow = report.HasTomorrow
                    ? string.Format(TomorrowFormat, CommunityBoard.ElementName(report.Tomorrow))
                    : TomorrowNone;
                forecastLabel.text = today + "\n" + tomorrow;
            }

            if (topLabel != null)
            {
                var sb = new StringBuilder(TopHeader);
                if (report.TopCards.Count == 0)
                {
                    sb.Append('\n').Append(TopEmpty);
                }
                else
                {
                    for (int i = 0; i < report.TopCards.Count; i++)
                    {
                        CardData card = report.TopCards[i];
                        int price = ctx.EconomyRules.GrossPrice(card, report.Today);
                        sb.Append('\n').Append(i + 1).Append(". ").Append(card.displayName).Append("  ").Append(price);
                    }
                }
                topLabel.text = sb.ToString();
            }

            if (moodLabel != null)
                moodLabel.text = string.Join("\n", report.MoodLines);
        }
    }
}
