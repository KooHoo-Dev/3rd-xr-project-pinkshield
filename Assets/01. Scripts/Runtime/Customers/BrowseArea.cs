using System;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 판매 진열대 구경 (11-19 손님 동선): 진열대 뒤 구매 자리 몇 칸 + 자리가 빌 때까지 창을 따라 서는 대기 줄.
    /// - 문을 들어온 손님: 기다리는 사람이 없고 빈 구매 자리가 있으면 바로 그 자리로, 아니면 대기 줄 끝으로 (새치기 없음)
    /// - 구매 자리가 비면(구경을 마치고 계산대로 감) 대기 줄 맨 앞 손님이 그 자리로 가고, 대기 줄이 한 칸씩 당겨짐
    /// 구경 시간 · 대사는 CustomerDirector가 정하고(startBrowse), 이 클래스는 자리와 걷기만 맡습니다.
    /// 대기 줄 걷기는 계산대 줄과 같은 <see cref="QueueMover"/>를 씁니다.
    /// </summary>
    public sealed class BrowseArea
    {
        readonly Func<int, Pose> spotPose;
        readonly int spotCount;
        readonly Func<CustomerRoutes> routes;
        readonly Action<CustomerAgent> startBrowse;
        readonly QueueMover waitLine;
        readonly CustomerAgent[] occupant;

        /// <param name="spotCount">구매 자리 수</param>
        /// <param name="spotPose">구매 자리 → 서는 곳 · 방향 (진열대 쪽을 봄)</param>
        /// <param name="waitPose">대기 줄 칸 번호 → 서는 곳 · 방향 (0번이 맨 앞)</param>
        /// <param name="startBrowse">구매 자리에 도착했을 때 (구경 시작)</param>
        public BrowseArea(int spotCount, Func<int, Pose> spotPose, Func<int, Pose> waitPose,
            Func<CustomerRoutes> routes, Action<CustomerAgent> startBrowse)
        {
            this.spotCount = spotCount;
            this.spotPose = spotPose;
            this.routes = routes;
            this.startBrowse = startBrowse;
            occupant = new CustomerAgent[spotCount];
            waitLine = new QueueMover(CustomerState.BrowseWait, waitPose, _ => PullNext());
        }

        /// <summary>대기 줄에 선(또는 걸어가는) 손님 수.</summary>
        public int Waiting => waitLine.Count;

        /// <summary>문 안에 들어온 손님을 구매 자리나 대기 줄로 보냅니다.</summary>
        public void Enter(CustomerAgent agent, IRandomSource random)
        {
            int spot = waitLine.Count == 0 ? PickFreeSpot(random) : -1;
            if (spot >= 0) GoToSpot(agent, spot);
            else waitLine.Join(agent, tail => routes().DoorToWait(tail));
        }

        /// <summary>구경을 마치고 떠남: 자리를 비우고 대기 줄 맨 앞 손님을 부릅니다.</summary>
        public void Release(CustomerAgent agent)
        {
            for (int i = 0; i < occupant.Length; i++)
                if (occupant[i] == agent) occupant[i] = null;
            agent.BrowseSlot = -1;
            waitLine.Forget(agent);
            PullNext();
        }

        /// <summary>영업 끝 · 정리: 자리와 대기 줄을 비움 (손님 오브젝트는 부르는 쪽이 지움).</summary>
        public void Clear()
        {
            Array.Clear(occupant, 0, occupant.Length);
            waitLine.Clear();
        }

        // 대기 줄 맨 앞 손님이 줄에 도착해 있고 빈 자리가 있으면 그 자리로 (가까운 오른쪽 자리부터)
        void PullNext()
        {
            CustomerAgent front = waitLine.Front;
            if (front == null || !front.InLine) return;
            int spot = -1;
            for (int i = spotCount - 1; i >= 0; i--)
                if (occupant[i] == null) { spot = i; break; }
            if (spot < 0) return;
            waitLine.Leave(front); // 뒤 손님들이 한 칸씩 당겨짐
            GoToSpot(front, spot, fromWait: true);
        }

        int PickFreeSpot(IRandomSource random)
        {
            int free = 0;
            for (int i = 0; i < spotCount; i++) if (occupant[i] == null) free++;
            if (free == 0) return -1;
            int pick = random != null ? random.Range(0, free) : 0;
            for (int i = 0; i < spotCount; i++)
            {
                if (occupant[i] != null) continue;
                if (pick-- == 0) return i;
            }
            return -1;
        }

        void GoToSpot(CustomerAgent agent, int spot, bool fromWait = false)
        {
            occupant[spot] = agent;
            agent.BrowseSlot = spot;
            agent.State = CustomerState.Enter;
            Pose p = spotPose(spot);
            CustomerRoutes r = routes();
            Vector3[] path = fromWait ? r.WaitToBrowse(p.position) : r.DoorToBrowse(p.position);
            agent.WalkPath(path, p.rotation, () => startBrowse(agent));
        }
    }
}
