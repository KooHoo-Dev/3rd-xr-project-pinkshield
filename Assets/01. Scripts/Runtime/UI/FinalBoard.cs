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
            // 가구 값 · 진열장 줄이 늘어도 판 밖으로 넘치지 않게 (원래 크기보다 커지지는 않음, 씬 다시 빌드 없이)
            if (body != null && !body.enableAutoSizing) { body.fontSizeMax = body.fontSize; body.fontSizeMin = 18f; body.enableAutoSizing = true; }
            if (body != null) body.text =ResultTexts.Final(r, success ? Context.Score.successEnding : Context.Score.failEnding);
        }
    }
}
