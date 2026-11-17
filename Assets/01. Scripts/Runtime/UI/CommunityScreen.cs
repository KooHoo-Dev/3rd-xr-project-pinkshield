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
    public sealed class CommunityScreen : MonoBehaviour
    {
        // 문구는 한 곳에 모읍니다.
        const string TodayFormat = "오늘 인기  <color=#FFC857>{0}</color>";
        const string TomorrowFormat = "내일 예보  <color=#7FD1FF>{0}</color>";
        const string TomorrowNone = "내일 예보  없음 (마지막 날)";
        const string TopHeader = "인기 카드 TOP3";
        const string TopEmpty = "아직 가진 인기 카드가 없어요";

        [Tooltip("화면 내용을 담은 오브젝트. 집이 아닐 때 꺼집니다.")]
        [SerializeField] GameObject content;
        [SerializeField] TMP_Text forecastLabel;
        [SerializeField] TMP_Text topLabel;
        [SerializeField] TMP_Text moodLabel;

        GameContext subscribed;
        Collection collection;
        bool dirty;

        void OnEnable()
        {
            subscribed = GameContext.Instance;
            if (subscribed != null)
            {
                subscribed.Events.PhaseChanged += OnPhaseChanged;
                subscribed.Events.SessionStarted += OnSessionStarted;
                Bind(subscribed.Session);
            }
            SetVisible(false);
        }

        void OnDisable()
        {
            if (subscribed != null)
            {
                subscribed.Events.PhaseChanged -= OnPhaseChanged;
                subscribed.Events.SessionStarted -= OnSessionStarted;
            }
            Bind(null);
            subscribed = null;
        }

        void OnSessionStarted(GameSession session) => Bind(session);

        // 집에서 카드를 팔거나 진열하면 TOP3(보유 카드 기준)가 달라지므로 다시 그림
        void Bind(GameSession session)
        {
            if (collection != null) collection.Changed -= OnCollectionChanged;
            collection = session?.Collection;
            if (collection != null) collection.Changed += OnCollectionChanged;
        }

        void OnCollectionChanged(string _) => dirty = true; // 한 프레임에 한 번만

        void LateUpdate()
        {
            if (dirty && content != null && content.activeSelf) Refresh();
        }

        void OnPhaseChanged(GamePhaseId from, GamePhaseId to)
        {
            bool home = to == GamePhaseId.Home;
            if (home) Refresh();
            SetVisible(home);
        }

        void SetVisible(bool visible)
        {
            if (content != null) content.SetActive(visible);
        }

        void Refresh()
        {
            dirty = false;
            GameContext ctx = GameContext.Instance;
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
