using CardShop.Core;
using CardShop.Core.Phases;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 1일차 임시 패널: 현재 페이즈 · 일차 · 재화 · 영업 남은 시간 표시 + "다음" 버튼(Ray로 누름).
    /// 각 페이즈의 실제 UI(타이틀, 정산, 퇴근 버튼)가 생기면 제거하거나 개발 빌드 전용으로 둡니다.
    /// </summary>
    public sealed class PhaseDebugPanel : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [SerializeField] TMP_Text label;
        [SerializeField] Button advanceButton;

        string lastNotice = "";
        GameContext subscribed;

        void OnEnable()
        {
            if (advanceButton != null) advanceButton.onClick.AddListener(OnAdvance);
            subscribed = GameContext.Instance;
            if (subscribed != null) subscribed.Events.Notice += OnNotice;
        }

        void OnDisable()
        {
            if (advanceButton != null) advanceButton.onClick.RemoveListener(OnAdvance);
            if (subscribed != null) subscribed.Events.Notice -= OnNotice;
        }

        void OnNotice(string message) => lastNotice = message;

        void OnAdvance() => flow.RequestAdvance();

        void Update()
        {
            GameContext ctx = GameContext.Instance;
            if (ctx == null || flow == null || label == null || ctx.Session == null) return;

            string text = $"{flow.CurrentPhase}\n{ctx.Session.Day}일차 / {ctx.Campaign.DayCount}일\n재화 {ctx.Session.Wallet.Money}";
            if (flow.Current is BusinessPhase business)
            {
                text += business.IsCleaningUp
                    ? $"\n마감 정리 {Mathf.CeilToInt(business.CleanupTimeLeft)}초"
                    : $"\n남은 시간 {Mathf.CeilToInt(business.TimeLeft)}초";
                int pending = flow.Cleanup?.PendingCount ?? 0;
                if (pending > 0) text += $" · 청소 {pending}곳";
            }
            if (flow.CurrentPhase == GamePhaseId.AfterHours)
                text += $"\n다음 팩 {ctx.PackOpening.NextSinglePackCost} · 도감 {ctx.Session.Collection.DiscoveredCount}/{ctx.Cards.Count}";
            if (flow.IsPaused)
                text += "\n(일시정지)";
            if (lastNotice.Length > 0)
                text += $"\n<size=70%>{lastNotice}</size>";
            label.text = text;
        }
    }
}
