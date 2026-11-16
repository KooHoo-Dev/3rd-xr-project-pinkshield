using CardShop.Core;
using CardShop.Logic;
using CardShop.Presentation;
using UnityEngine;

namespace CardShop.Interaction
{
    /// <summary>
    /// 개봉 요청을 받아 규칙(PackOpeningService)을 실행하고, 결과를 연출(CardRevealer)에 넘깁니다.
    /// 개봉 가능 시점: 퇴근 전(AfterHours). 사장님 선물 팩은 1일차 인트로에서도 가능.
    /// </summary>
    public sealed class PackOpener : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [SerializeField] CardRevealer revealer;

        public void Configure(GameFlow gameFlow, CardRevealer cardRevealer)
        {
            flow = gameFlow;
            revealer = cardRevealer;
        }

        public void TryOpen(PackView pack)
        {
            GameContext ctx = GameContext.Instance;
            bool allowed = flow.CurrentPhase == GamePhaseId.AfterHours ||
                           (pack.IsGift && flow.CurrentPhase == GamePhaseId.Intro);
            if (!allowed)
            {
                ctx.Events.RaiseNotice("팩은 영업이 끝난 뒤에 열 수 있어요.");
                return;
            }

            int cost = pack.IsGift ? 0 : ctx.PackOpening.NextSinglePackCost;
            if (!ctx.PackOpening.TryOpenSingle(pack.Clue, pack.IsGift, out PackResult result, out OpenFailReason fail))
            {
                if (fail == OpenFailReason.NotEnoughMoney)
                    ctx.Events.RaiseNotice($"재화가 부족해요. (팩 {ctx.Economy.packPrice}, 보유 {ctx.Session.Wallet.Money})");
                return;
            }

            pack.Consume();
            ctx.Events.RaiseNotice(cost == 0 ? "무료 팩 개봉!" : $"팩 개봉 (-{cost})");
            ctx.Events.RaisePacksOpened(new[] { result });
            revealer.RevealPack(result);
        }

        /// <summary>상자 개봉 버튼(Poke).</summary>
        public void TryOpenBox()
        {
            GameContext ctx = GameContext.Instance;
            if (flow.CurrentPhase != GamePhaseId.AfterHours)
            {
                ctx.Events.RaiseNotice("상자는 영업이 끝난 뒤에 열 수 있어요.");
                return;
            }
            if (!ctx.PackOpening.TryOpenBox(out PackResult[] results, out OpenFailReason fail))
            {
                if (fail == OpenFailReason.NotEnoughMoney)
                    ctx.Events.RaiseNotice($"재화가 부족해요. (상자 {ctx.Economy.boxPrice}, 보유 {ctx.Session.Wallet.Money})");
                return;
            }

            ctx.Events.RaisePacksOpened(results);
            revealer.RevealBox(results);
        }
    }
}
