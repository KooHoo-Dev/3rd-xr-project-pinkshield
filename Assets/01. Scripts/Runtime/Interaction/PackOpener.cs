using System.Collections;
using CardShop.Core;
using CardShop.Logic;
using CardShop.Presentation;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>
    /// 개봉 요청을 받아 규칙(PackOpeningService)을 실행하고, 결과를 연출(CardRevealer)에 넘깁니다.
    /// 개봉 가능 시점: 퇴근 전(AfterHours). 사장님 선물 팩은 1일차 인트로에서도 가능.
    /// 열 수 없을 때는 말풍선 + 거부 진동(짧게 2번)으로 알립니다. 디버그 패널용 Notice도 그대로 보냅니다.
    /// </summary>
    public sealed class PackOpener : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [SerializeField] CardRevealer revealer;

        WorldToast toast;

        public void Configure(GameFlow gameFlow, CardRevealer cardRevealer)
        {
            flow = gameFlow;
            revealer = cardRevealer;
        }

        public void TryOpen(PackView pack)
        {
            GameContext ctx = GameContext.Instance;
            XRBaseInputInteractor hand = HandHolding(pack);
            Vector3 toastPos = pack.transform.position + Vector3.up * 0.12f;

            bool allowed = flow.CurrentPhase == GamePhaseId.AfterHours ||
                           (pack.IsGift && flow.CurrentPhase == GamePhaseId.Intro);
            if (!allowed) { Deny("팩은 영업이 끝난 뒤에 열 수 있어요.", toastPos, hand); return; }
            if (revealer.IsBusy) { Deny("상자를 여는 중이에요.", toastPos, hand); return; }

            int cost = pack.IsGift ? 0 : ctx.PackOpening.NextSinglePackCost;
            if (!ctx.PackOpening.TryOpenSingle(pack.Clue, pack.IsGift, out PackResult result, out OpenFailReason fail))
            {
                if (fail == OpenFailReason.NotEnoughMoney)
                    Deny($"재화가 부족해요. (팩 {ctx.Economy.packPrice}, 보유 {ctx.Session.Wallet.Money})", toastPos, hand);
                return;
            }

            if (hand != null) hand.SendHapticImpulse(0.6f, 0.1f); // 개봉 손맛
            var stackPose = new Pose(pack.transform.position, pack.transform.rotation); // 팩이 있던 자리(눈앞)에 카드 묶음
            pack.Consume();
            ctx.Events.RaiseNotice(cost == 0 ? "무료 팩 개봉!" : $"팩 개봉 (-{cost})");
            ctx.Events.RaisePacksOpened(new[] { result });
            revealer.RevealPack(result, stackPose);
        }

        /// <summary>상자 개봉 버튼(Poke).</summary>
        public void TryOpenBox()
        {
            GameContext ctx = GameContext.Instance;
            Vector3 toastPos = revealer.AnchorPosition + Vector3.up * 0.1f;

            if (flow.CurrentPhase != GamePhaseId.AfterHours) { Deny("상자는 영업이 끝난 뒤에 열 수 있어요.", toastPos, null); return; }
            if (revealer.IsBusy) { Deny("상자를 여는 중이에요.", toastPos, null); return; }
            if (!ctx.PackOpening.TryOpenBox(out PackResult[] results, out OpenFailReason fail))
            {
                if (fail == OpenFailReason.NotEnoughMoney)
                    Deny($"재화가 부족해요. (상자 {ctx.Economy.boxPrice}, 보유 {ctx.Session.Wallet.Money})", toastPos, null);
                return;
            }

            ctx.Events.RaisePacksOpened(results);
            revealer.RevealBox(results);
        }

        void Deny(string message, Vector3 at, XRBaseInputInteractor hand)
        {
            GameContext.Instance.Events.RaiseNotice(message);
            if (toast == null) toast = WorldToast.Create(transform, revealer.Font);
            toast.Show(message, at);
            if (hand != null) StartCoroutine(DenyPulse(hand));
        }

        static IEnumerator DenyPulse(XRBaseInputInteractor hand)
        {
            hand.SendHapticImpulse(0.4f, 0.05f);
            yield return new WaitForSeconds(0.1f);
            if (hand != null) hand.SendHapticImpulse(0.4f, 0.05f);
        }

        static XRBaseInputInteractor HandHolding(PackView pack)
        {
            var selecting = pack.Grab.interactorsSelecting;
            return selecting.Count > 0 ? selecting[0] as XRBaseInputInteractor : null;
        }
    }
}
