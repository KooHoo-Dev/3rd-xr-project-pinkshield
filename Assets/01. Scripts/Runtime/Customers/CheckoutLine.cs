using System;
using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 계산대 줄 하나의 흐름: 줄 서기 → 맨 앞이면 계산 시작(CheckoutCounter) → 결제 인사 또는 인내 시간 초과로 화내기 → 걸어 나가기.
    /// CustomerDirector에서 떼어 냄 (큰_클래스_나누기_제안 2장). 걷기 · 칸 당기기는 <see cref="QueueMover"/>,
    /// 인내 시간 판단은 <see cref="PatienceRules"/>, 말풍선 · 나가는 길 · 손님 지우기는 Director가 넘긴 함수로 합니다.
    /// </summary>
    public sealed class CheckoutLine
    {
        /// <summary>Director가 넘기는 것들 (이 클래스는 Director를 모름).</summary>
        public sealed class Hooks
        {
            public Func<GameContext> Context;
            public Func<CustomerLineSet> LineSet;
            public Action<CustomerAgent, CustomerMoment, string> Say;
            public Func<float, bool> Chance;
            /// <summary>(손님, 빠지기 전 칸 번호) → 문 밖까지 걷는 길</summary>
            public Func<CustomerAgent, int, Vector3[]> LeavePath;
            /// <summary>문 밖에 도착해 사라질 때</summary>
            public Action<CustomerAgent> Left;
            public Func<float> ReactionSeconds;
            public Func<float> AngrySeconds;
        }

        readonly QueueMover queue;
        readonly CheckoutCounter counter;
        readonly Hooks hooks;

        // 지금 계산대에 물건을 올린 손님 (줄 맨 앞 한 명뿐)
        CustomerAgent checkoutAgent;

        public CheckoutLine(Func<int, Pose> slotPose, CheckoutCounter counter, Hooks hooks)
        {
            this.counter = counter;
            this.hooks = hooks;
            queue = new QueueMover(CustomerState.Queue, slotPose, PrepareCheckout);
        }

        public IReadOnlyList<CustomerAgent> Members => queue.Members;

        /// <summary>줄 끝에 섭니다. pathTo(줄 끝 자리) = 지금 자리에서 그곳까지의 길.</summary>
        public void Join(CustomerAgent agent, Func<Vector3, Vector3[]> pathTo)
        {
            agent.QueueSeconds = 0f;
            queue.Join(agent, pathTo);
        }

        /// <summary>매 프레임 (일시정지 중에는 부르지 않음): 인내 시간 대사 · 넘으면 화내고 떠남 (감점 없음, 설계서 Q2).</summary>
        public void Tick()
        {
            CustomerLineSet set = hooks.LineSet();
            float waitingAt = set != null ? set.waitingAt : 0.35f;
            float impatientAt = set != null ? set.impatientAt : 0.75f;
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                CustomerAgent a = queue.Members[i];
                if (a.State != CustomerState.Queue) continue;
                PatienceStage stage = PatienceRules.Stage(a.QueueSeconds, Patience(a), waitingAt, impatientAt);
                if (stage == PatienceStage.Expired) { LeaveAngry(a); continue; }
                switch (PatienceRules.LineToSay(stage, a.SaidWaiting, a.SaidImpatient))
                {
                    case PatienceStage.Impatient:
                        a.SaidImpatient = a.SaidWaiting = true;
                        hooks.Say(a, CustomerMoment.Impatient, null);
                        break;
                    case PatienceStage.Waiting:
                        a.SaidWaiting = true;
                        if (a != checkoutAgent && hooks.Chance(set != null ? set.waitingChance : 0.5f)) hooks.Say(a, CustomerMoment.Waiting, null);
                        break;
                }
            }
        }

        /// <summary>도둑을 잡음: 줄 선 손님이 박수 치고 인내 시간을 되돌려 받음 (카드도둑_설계 2장).</summary>
        public void Cheer(float bonusSeconds)
        {
            foreach (CustomerAgent a in queue.Members)
            {
                if (a.State != CustomerState.Queue) continue;
                a.QueueSeconds = Mathf.Max(0f, a.QueueSeconds - bonusSeconds);
                a.Gesture(CustomerAnim.Cheer);
            }
        }

        /// <summary>장부에서만 지움 (이미 떠난 손님).</summary>
        public void Forget(CustomerAgent agent) => queue.Forget(agent);

        /// <summary>계산을 취소하고 줄을 비움 (손님 오브젝트는 Director가 지움).</summary>
        public void Clear()
        {
            CancelCheckout();
            queue.Clear();
        }

        // 계산 중인 손님은 물건이 2개째부터 한 개당 더 기다려 줌 (계산시스템_확장_설계 K6)
        float Patience(CustomerAgent agent)
        {
            GameContext ctx = hooks.Context();
            if (ctx == null) return 30f;
            bool atCheckout = agent == checkoutAgent && counter != null;
            // 침대 업그레이드: 푹 자고 나온 날은 손님을 더 기다리게 해도 됨 (집_업그레이드_설계 3장)
            float rested = ctx.Upgrades != null && ctx.Session != null ? ctx.Upgrades.PatienceMultiplier(ctx.Session.Upgrades) : 1f;
            return PatienceRules.Seconds(ctx.Campaign.customerPatienceSeconds * rested, ctx.Campaign.patiencePerExtraItemSeconds,
                atCheckout ? counter.ItemCount : 0, atCheckout);
        }

        // 줄 맨 앞에 도착한 손님이 장바구니를 카운터에 올림. 무엇을 몇 개 살지 · 바코드 · 결제는 CheckoutCounter, 끝나면 OnPaid
        void PrepareCheckout(CustomerAgent agent)
        {
            if (checkoutAgent == agent || !queue.IsFront(agent) || agent.State != CustomerState.Queue) return;
            agent.Gesture(CustomerAnim.Talk);
            hooks.Say(agent, CustomerMoment.Checkout, null);
            checkoutAgent = agent;
            if (counter != null) counter.Begin((bought, count, net) => OnPaid(agent, bought, count, net));
            else Debug.LogWarning("[CheckoutLine] CheckoutCounter가 없어 계산할 수 없습니다 (Build Shop Scene 다시 실행).");
        }

        void OnPaid(CustomerAgent agent, CardData bought, int itemCount, int net)
        {
            checkoutAgent = null;
            agent.State = CustomerState.Leave; // 인사하는 동안은 맨 앞 칸을 계속 차지 (다음 손님이 겹쳐 들어오지 않게)
            GameContext ctx = hooks.Context();

            if (bought != null)
            {
                ctx.Events.RaiseNotice($"손님이 판매 등록한 「{bought.displayName}」을(를) 샀어요! (정산 때 +{net})");
                agent.Gesture(CustomerAnim.Cheer);
                hooks.Say(agent, CustomerMoment.PaidCard, bought.displayName);
            }
            else
            {
                ctx.Events.RaiseNotice(itemCount > 1 ? $"손님이 가게 재고 {itemCount}개를 사 갔어요." : "손님이 가게 재고를 사 갔어요.");
                agent.Gesture(CustomerAnim.Nod);
                hooks.Say(agent, CustomerMoment.Paid, null);
            }

            agent.Wait(hooks.ReactionSeconds(), () => Leave(agent, wave: true));
        }

        void LeaveAngry(CustomerAgent agent)
        {
            if (agent == checkoutAgent) CancelCheckout(); // 물건을 두고 가지 않음, 판매도 없음
            GameContext ctx = hooks.Context();
            ctx.Session.Ledger.customersLost++;
            agent.State = CustomerState.Leave; // 화내는 동안은 자기 칸을 계속 차지
            agent.Gesture(CustomerAnim.Angry);
            hooks.Say(agent, CustomerMoment.Angry, null);
            ctx.Events.RaiseNotice("손님이 기다리다 그냥 나갔어요.");
            agent.Wait(hooks.AngrySeconds(), () => Leave(agent, wave: false));
        }

        // 줄에서 빠져 걸어 나가기 시작: 이때 칸이 비므로 줄을 당김
        void Leave(CustomerAgent agent, bool wave)
        {
            int index = queue.Leave(agent);
            hooks.Context()?.Events.RaiseCustomerLeaving(agent.transform);
            if (wave) agent.Gesture(CustomerAnim.Wave);
            agent.WalkPath(hooks.LeavePath(agent, index), null, () => hooks.Left(agent));
        }

        void CancelCheckout()
        {
            checkoutAgent = null;
            if (counter != null) counter.Cancel();
        }
    }
}
