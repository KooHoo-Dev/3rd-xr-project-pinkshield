using System.Collections.Generic;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 판매 진열대 위 팩 꽂이 (기획서 5-1 "흐트러진 팩 정리: 흩어진 팩을 집어 진열대 칸에 꽂기").
    /// 칸마다 팩이 하나씩 꽂혀 있고, 손님이 흐트러뜨리면 팩 몇 개가 바닥(CleanupDirector가 고른 자리 근처)으로 떨어집니다.
    /// 빈 칸 근처에서 팩을 놓으면 그 칸에 꽂힙니다(소켓 대신 거리 판정: 어느 빈 칸이든 됨).
    /// 팩을 들고 있는 동안 빈 칸마다 윤곽(DropHint)이 뜨고, 꽂히는 거리면 그 칸이 초록으로 바뀝니다 (3차 기기 피드백).
    /// </summary>
    public sealed class PackRack : MonoBehaviour
    {
        [Tooltip("칸 자리. forward = 팩 앞면 방향, 원점 = 팩이 꽂히는 위치")]
        [SerializeField] Transform[] slots;
        [Tooltip("처음 꽂혀 있는 팩 (slots와 같은 순서)")]
        [SerializeField] RackPack[] packs;
        [Tooltip("빈 칸에서 이 거리 안에 놓으면 꽂힘 (m)")]
        [SerializeField] float snapDistance = 0.18f;
        [Tooltip("칸마다 빈 칸 윤곽 (slots와 같은 순서). 비워 두면 윤곽 없음")]
        [SerializeField] DropHint[] slotHints;
        [Tooltip("팩을 들고 있을 때 꽂이 위에 뜨는 안내. 비워 두면 안내 없음")]
        [SerializeField] DropHint rackHint;
        [Tooltip("바닥에 떨어진 팩 높이 (눕혔을 때 두께의 절반, m)")]
        [SerializeField] float floorHeight = 0.006f;

        public void ConfigureHints(DropHint[] perSlot, DropHint overall)
        {
            slotHints = perSlot;
            rackHint = overall;
        }

        void Awake()
        {
            for (int i = 0; i < packs.Length; i++)
            {
                packs[i].Init(this, i);
                packs[i].Held.LandHeight = floorHeight;
                packs[i].Held.LandFlat = true;
                packs[i].Held.SetGrabbable(false);
            }
        }

        public int InPlaceCount
        {
            get { int n = 0; foreach (RackPack p in packs) if (p.InRack) n++; return n; }
        }

        /// <summary>꽂힌 팩 중 최대 count개를 center 둘레 바닥에 흩어 떨어뜨리고, 떨어뜨린 팩을 돌려줍니다.</summary>
        public List<RackPack> Scatter(Vector3 center, int count, IRandomSource random)
        {
            var inRack = new List<RackPack>();
            foreach (RackPack p in packs) if (p.InRack) inRack.Add(p);
            var scattered = new List<RackPack>();
            float startAngle = (float)(random.NextDouble() * 360.0);
            for (int i = 0; i < count && inRack.Count > 0; i++)
            {
                RackPack p = inRack[random.Range(0, inRack.Count)];
                inRack.Remove(p);
                p.Slot = -1;
                // 가운데 둘레에 고르게 (각도를 나눠 서로 겹치지 않게), 반경 0.15~0.4m, 가게 벽 안쪽
                float angle = startAngle + i * (360f / Mathf.Max(1, count)) + (float)(random.NextDouble() * 40.0 - 20.0);
                float dist = 0.15f + (float)random.NextDouble() * 0.25f;
                Vector3 pos = center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist;
                pos = ClampInsideShop(pos);
                pos.y = floorHeight;
                Quaternion rot = Quaternion.Euler(90f, (float)(random.NextDouble() * 360.0), 0f);
                p.Held.PlaceAt(pos, rot);
                p.Held.SetGrabbable(true);
                scattered.Add(p);
            }
            return scattered;
        }

        /// <summary>일시정지 중이면 true (CleanupDirector가 넣음). 마감 시간이 멈춘 동안 청소를 받지 않음</summary>
        public System.Func<bool> IsPaused { get; set; }

        /// <summary>가장 가까운 빈 칸이 놓은 위치에서 snapDistance 안이면 꽂습니다.</summary>
        public bool TryReturn(RackPack pack)
        {
            if (IsPaused != null && IsPaused()) return false;
            int best = NearestEmptySlot(pack.transform.position);
            if (best < 0) return false;
            Seat(pack, best);
            return true;
        }

        int NearestEmptySlot(Vector3 position)
        {
            int best = -1;
            float bestSqr = snapDistance * snapDistance;
            for (int i = 0; i < slots.Length; i++)
            {
                if (Occupied(i)) continue;
                float d = (slots[i].position - position).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; best = i; }
            }
            return best;
        }

        /// <summary>하루 끝: 흐트러진 팩을 모두 빈 칸에 다시 꽂습니다.</summary>
        public void ResetAll()
        {
            foreach (RackPack p in packs)
            {
                if (p.InRack) continue;
                for (int i = 0; i < slots.Length; i++)
                    if (!Occupied(i)) { Seat(p, i); break; }
            }
        }

        // 들고 있는 팩이 있으면 빈 칸 윤곽을 보여 주고, 꽂히는 칸은 초록
        void Update()
        {
            int readySlot = -1;
            bool carrying = false;
            foreach (RackPack p in packs)
            {
                if (p.InRack || !p.Held.IsHeld) continue;
                carrying = true;
                int s = NearestEmptySlot(p.transform.position);
                if (s >= 0) readySlot = s;
            }
            if (rackHint != null)
            {
                rackHint.SetShown(carrying);
                rackHint.SetReady(readySlot >= 0);
            }
            if (slotHints == null) return;
            for (int i = 0; i < slotHints.Length && i < slots.Length; i++)
            {
                if (slotHints[i] == null) continue;
                slotHints[i].SetShown(carrying && !Occupied(i));
                slotHints[i].SetReady(i == readySlot);
            }
        }

        bool Occupied(int slot)
        {
            foreach (RackPack p in packs) if (p.Slot == slot) return true;
            return false;
        }

        void Seat(RackPack pack, int slot)
        {
            pack.Slot = slot;
            // 잡기를 먼저 끔: 들고 있는 팩이면 강제로 놓이며 떨어지기 시작하는데, 그 뒤 PlaceAt이 떨어짐을 멈추고 칸에 꽂음
            pack.Held.SetGrabbable(false);
            pack.Held.PlaceAt(slots[slot].position, slots[slot].rotation);
        }

        static Vector3 ClampInsideShop(Vector3 p)
        {
            const float margin = 0.4f;
            p.x = Mathf.Clamp(p.x, ShopLayout.MinX + ShopLayout.WallHalf + margin, ShopLayout.MaxX - ShopLayout.WallHalf - margin);
            p.z = Mathf.Clamp(p.z, ShopLayout.BackZ + ShopLayout.WallHalf + margin, ShopLayout.FrontZ - ShopLayout.WallHalf - margin);
            return p;
        }
    }
}
