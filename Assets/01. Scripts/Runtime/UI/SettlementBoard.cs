using CardShop.Cards;
using CardShop.Core;
using CardShop.Core.Phases;
using CardShop.Logic;
using CardShop.Presentation;

namespace CardShop.UI
{
    /// <summary>일일 정산 판: 기본급 · 청소 · 계산 평가 · 판매 수익 · 합계 · 보유 금액 + [확인] → 퇴근 전 시간.</summary>
    public sealed class SettlementBoard : PhaseBoard
    {
        protected override GamePhaseId Phase => GamePhaseId.Settlement;

        // 정산 판이 뜨면 돈 들어오는 소리 (SoundBank 칸이 비면 조용)
        protected override void OnPhaseChanged(GamePhaseId phase)
        {
            base.OnPhaseChanged(phase);
            if (phase == GamePhaseId.Settlement) Sfx.Play(SoundId.MoneyIn, transform.position);
        }

        protected override void Redraw()
        {
            var phase = Flow.GetPhase<SettlementPhase>(GamePhaseId.Settlement);
            GameSession s = Context.Session;
            if (heading != null) heading.text = ResultTexts.SettlementTitle(s.Day, Context.Campaign.DayCount);
            if (body != null) body.text = ResultTexts.Settlement(s.Ledger, Context.EconomyRules, phase != null ? phase.LastTotal : 0, s.Wallet.Money);
        }
    }
}
