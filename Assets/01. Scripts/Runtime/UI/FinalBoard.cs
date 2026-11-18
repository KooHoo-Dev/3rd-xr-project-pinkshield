using CardShop.Core;
using CardShop.Core.Phases;
using CardShop.Logic;

namespace CardShop.UI
{
    /// <summary>최종 정산 판 (기획서 5-8): 재화 · 카드 가치 · 도감 · 점수 · 랭크 · 엔딩 + [타이틀로].</summary>
    public sealed class FinalBoard : PhaseBoard
    {
        protected override GamePhaseId Phase => GamePhaseId.Final;

        protected override void Redraw()
        {
            FinalResult r = Flow.GetPhase<FinalPhase>(GamePhaseId.Final)?.Result;
            bool success = r != null && r.Rank != null && r.Rank.isSuccess;
            if (heading != null) heading.text = $"{Context.Campaign.DayCount}일 알바 끝!";
            if (body != null) body.text = ResultTexts.Final(r, success ? Context.Score.successEnding : Context.Score.failEnding);
        }
    }
}
