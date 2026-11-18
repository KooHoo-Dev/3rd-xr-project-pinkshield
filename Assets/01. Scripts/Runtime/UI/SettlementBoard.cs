using CardShop.Core;
using CardShop.Core.Phases;
using CardShop.Logic;

namespace CardShop.UI
{
    /// <summary>일일 정산 판: 기본급 · 청소 · 계산 평가 · 판매 수익 · 합계 · 보유 금액 + [확인] → 퇴근 전 시간.</summary>
    public sealed class SettlementBoard : PhaseBoard
    {
        protected override GamePhaseId Phase => GamePhaseId.Settlement;

        protected override void Redraw()
        {
            var phase = Flow.GetPhase<SettlementPhase>(GamePhaseId.Settlement);
            GameSession s = Context.Session;
            if (heading != null) heading.text = ResultTexts.SettlementTitle(s.Day, Context.Campaign.DayCount);
            if (body != null) body.text = ResultTexts.Settlement(s.Ledger, Context.EconomyRules, phase != null ? phase.LastTotal : 0, s.Wallet.Money);
        }
    }
}
