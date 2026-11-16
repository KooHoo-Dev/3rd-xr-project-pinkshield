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

        void OnEnable()
        {
            if (advanceButton != null) advanceButton.onClick.AddListener(OnAdvance);
        }

        void OnDisable()
        {
            if (advanceButton != null) advanceButton.onClick.RemoveListener(OnAdvance);
        }

        void OnAdvance() => flow.RequestAdvance();

        void Update()
        {
            GameContext ctx = GameContext.Instance;
            if (ctx == null || flow == null || label == null || ctx.Session == null) return;

            string text = $"{flow.CurrentPhase}\n{ctx.Session.Day}일차 / {ctx.Campaign.DayCount}일\n재화 {ctx.Session.Wallet.Money}";
            if (flow.Current is BusinessPhase business)
                text += $"\n남은 시간 {Mathf.CeilToInt(business.TimeLeft)}초";
            if (flow.IsPaused)
                text += "\n(일시정지)";
            label.text = text;
        }
    }
}
