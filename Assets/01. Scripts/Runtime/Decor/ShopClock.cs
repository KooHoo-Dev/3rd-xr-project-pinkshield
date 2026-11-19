using CardShop.Core;
using CardShop.Core.Phases;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Decor
{
    /// <summary>
    /// 벽시계 = 영업 진행도 (인테리어 설계 S1). 화면 고정 UI 대신 세계 안 물건으로 "영업이 얼마나 남았는지"를 보여 줍니다.
    /// 영업 시간(BusinessPhase 남은 시간)을 9시 ~ 16시로 옮겨 바늘을 돌립니다. 마감 청소 중에는 16시부터 1초 = 1분.
    /// 바늘은 시계 앞면(로컬 +Z가 보는 사람 쪽)에서 12시 방향이 로컬 +Y인 상태가 0도입니다.
    /// </summary>
    public sealed class ShopClock : PhaseListener
    {
        [SerializeField] Transform hourHand;
        [SerializeField] Transform minuteHand;
        // 영업 시각은 전광판 디지털 시계와 같게 9시 → 16시 (BusinessClockRules, 11-20 F6)
        const float openHour = BusinessClockRules.OpenHour;
        const float closeHour = BusinessClockRules.CloseHour;

        float shownHour = -1f;

        protected override void Refresh() => SetTime(HourNow());

        void Update()
        {
            if (CurrentPhase == GamePhaseId.Business) SetTime(HourNow());
        }

        float HourNow()
        {
            switch (CurrentPhase)
            {
                case GamePhaseId.Business:
                    var business = Flow != null ? Flow.GetPhase<BusinessPhase>(GamePhaseId.Business) : null;
                    float total = Context != null && Context.Session != null
                        ? Context.Campaign.GetDay(Context.Session.Day).businessSeconds : 0f;
                    if (business == null || total <= 0f) return openHour;
                    if (business.IsCleaningUp)
                        return BusinessClockRules.HourAfterClose(Context.Campaign.closingCleanupSeconds - business.CleanupTimeLeft);
                    return BusinessClockRules.HourAt(1f - business.TimeLeft / total);
                case GamePhaseId.Settlement:
                case GamePhaseId.AfterHours:
                case GamePhaseId.DayEnd:
                case GamePhaseId.Final:
                case GamePhaseId.Home:
                    return closeHour + 0.25f;
                default:
                    return openHour - 0.3f; // 영업 전
            }
        }

        void SetTime(float hour)
        {
            if (Mathf.Abs(hour - shownHour) < 0.001f) return;
            shownHour = hour;
            float hourDeg = (hour % 12f) / 12f * 360f;
            float minuteDeg = (hour % 1f) * 360f;
            // 보는 사람 쪽(+Z)에서 시계 방향 = +Z 축 기준 음의 각도
            if (hourHand != null) hourHand.localRotation = Quaternion.AngleAxis(-hourDeg, Vector3.forward);
            if (minuteHand != null) minuteHand.localRotation = Quaternion.AngleAxis(-minuteDeg, Vector3.forward);
        }
    }
}
