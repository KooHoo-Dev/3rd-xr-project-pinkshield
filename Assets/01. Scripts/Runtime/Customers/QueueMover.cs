using System;
using System.Collections.Generic;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 줄 하나를 실제로 걷게 합니다: 줄 끝으로 걸어가기 · 칸 도착 · 앞사람이 빠지면 한 칸씩 당기기.
    /// 칸 순서 규칙은 <see cref="CustomerLine{T}"/>(순수, 테스트), 걷는 길은 부르는 쪽이 넘깁니다.
    /// CustomerDirector에서 떼어 냄 (큰_클래스_나누기_제안 2장). 계산대 줄과 구매 자리 대기 줄(11-19 손님 동선)이 같이 씁니다.
    ///
    /// 규칙 (11-18 기기 테스트: 손님이 겹쳐 섬):
    /// - 떠나는 손님은 <see cref="Leave"/>로 걸어 나가기 시작할 때까지 자기 칸을 차지합니다.
    /// - 줄로 걸어오는 중(InLine = false)인 손님은 줄이 당겨져도 길을 바꾸지 않고, 도착한 뒤 줄을 따라 지금 칸으로 갑니다.
    /// </summary>
    public sealed class QueueMover
    {
        readonly CustomerLine<CustomerAgent> line = new CustomerLine<CustomerAgent>();
        readonly CustomerState state;
        readonly Func<int, Pose> slotPose;
        readonly Action<CustomerAgent> arrivedAtFront;

        /// <param name="state">이 줄에 선 손님의 상태 (다른 상태가 되면 도착 처리를 하지 않음)</param>
        /// <param name="slotPose">칸 번호 → 서는 자리 · 방향</param>
        /// <param name="arrivedAtFront">맨 앞 칸에 도착했을 때</param>
        public QueueMover(CustomerState state, Func<int, Pose> slotPose, Action<CustomerAgent> arrivedAtFront)
        {
            this.state = state;
            this.slotPose = slotPose;
            this.arrivedAtFront = arrivedAtFront;
        }

        public int Count => line.Count;
        public IReadOnlyList<CustomerAgent> Members => line.Members;
        public bool IsFront(CustomerAgent agent) => line.IsFront(agent);
        public CustomerAgent Front => line.Count > 0 ? line.Members[0] : null;

        /// <summary>줄 끝에 섭니다. pathTo(줄 끝 자리) = 지금 자리에서 그곳까지의 길 (마지막 점이 줄 끝).</summary>
        public void Join(CustomerAgent agent, Func<Vector3, Vector3[]> pathTo)
        {
            agent.State = state;
            agent.InLine = false;
            int index = line.Join(agent);
            Pose tail = slotPose(index);
            agent.WalkPath(pathTo(tail.position), tail.rotation, () => Arrive(agent));
        }

        /// <summary>줄에서 빠져 걸어 나가기 시작: 칸이 비므로 뒤 손님을 당깁니다. 빠지기 전 칸 번호(없었으면 -1).</summary>
        public int Leave(CustomerAgent agent)
        {
            int index = line.Leave(agent);
            if (index >= 0) Shift();
            return index;
        }

        /// <summary>당기지 않고 장부에서만 지움 (이미 떠난 손님 정리).</summary>
        public void Forget(CustomerAgent agent) => line.Leave(agent);

        public void Clear() => line.Clear();

        // 칸에 도착: 그새 줄이 당겨졌으면 줄을 따라 지금 칸으로, 맨 앞이면 알림
        void Arrive(CustomerAgent agent)
        {
            if (agent.State != state) return;
            agent.InLine = true;
            int index = line.SlotOf(agent);
            if (index < 0) return;
            Pose p = slotPose(index);
            Vector3 flat = agent.transform.position - p.position;
            flat.y = 0f;
            if (flat.magnitude > 0.05f) { agent.WalkTo(p.position, p.rotation, () => Arrive(agent)); return; }
            if (index == 0) arrivedAtFront?.Invoke(agent);
        }

        // 이미 줄에 선 손님은 한 칸씩 앞으로. 걸어오는 중인 손님은 도착한 뒤 Arrive가 맞춤
        void Shift()
        {
            for (int i = 0; i < line.Count; i++)
            {
                CustomerAgent a = line.Members[i];
                if (a.State != state || !a.InLine) continue;
                Pose p = slotPose(i);
                a.WalkTo(p.position, p.rotation, () => Arrive(a));
            }
        }
    }
}
