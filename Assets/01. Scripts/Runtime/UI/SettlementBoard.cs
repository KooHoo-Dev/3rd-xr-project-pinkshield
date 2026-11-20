using CardShop.Cards;
using CardShop.Core;
using CardShop.Core.Phases;
using CardShop.Logic;
using CardShop.Presentation;

namespace CardShop.UI
{
    /// <summary>
    /// 일일 정산 판: 기본급 · 청소 · 계산 평가 · 판매 수익 · 합계 · 보유 금액 + [확인] → 퇴근 전 시간.
    /// UI_개편_설계 C안 4번 판: 영수증 재질, 한 줄씩 인쇄되고 마지막에 도장 (ReceiptPrinter).
    /// </summary>
    public sealed class SettlementBoard : PhaseBoard
    {
        const string ShopHeader = "<size=50%><color=#6B6080>SLIME CARDSHOP · 알바 정산서</color></size>\n";

        ReceiptPrinter printer;

        protected override GamePhaseId Phase => GamePhaseId.Settlement;

        // 씬(Shop.unity)은 그대로 두고 실행할 때 재질을 입힘 (11-20부터 씬이 원본)
        void Awake()
        {
            if (content == null) return;
            UiSkin.ApplyPanel(content.transform, UiSurface.Receipt);
            if (heading != null)
            {
                heading.enableAutoSizing = true;
                heading.fontSizeMax = heading.fontSize;
                heading.fontSizeMin = 20f;
            }
            printer = gameObject.AddComponent<ReceiptPrinter>().Setup(content.transform, body, body != null ? body.font : null);
        }

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
            if (heading != null) heading.text = ShopHeader + ResultTexts.SettlementTitle(s.Day, Context.Campaign.DayCount);
            if (body != null) body.text = ResultTexts.Settlement(s.Ledger, Context.EconomyRules, phase != null ? phase.LastTotal : 0, s.Wallet.Money);
            if (printer != null) printer.Print(SettlementStamp.For(s.Ledger));
        }
    }
}
