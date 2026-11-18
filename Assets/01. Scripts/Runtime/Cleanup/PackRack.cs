using System.Collections.Generic;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 판매 진열대 위 팩 꽂이 (기획서 5-1 "흐트러진 팩 정리: 흩어진 팩을 집어 진열대 칸에 꽂기").
    /// 칸마다 팩이 하나씩 꽂혀 있고, 손님이 흐트러뜨리면 팩 몇 개가 근처 바닥으로 떨어집니다.
    /// 빈 칸 근처에서 팩을 놓으면 그 칸에 꽂힙니다(소켓 대신 거리 판정: 어느 빈 칸이든 됨).
    /// </summary>
    public sealed class PackRack : MonoBehaviour
    {
        [Tooltip("칸 자리. forward = 팩 앞면 방향, 원점 = 팩이 꽂히는 위치")]
        [SerializeField] Transform[] slots;
        [Tooltip("처음 꽂혀 있는 팩 (slots와 같은 순서)")]
        [SerializeField] RackPack[] packs;
        [Tooltip("빈 칸에서 이 거리 안에 놓으면 꽂힘 (m)")]
        [SerializeField] float snapDistance = 0.18f;

        void Awake()
        {
            for (int i = 0; i < packs.Length; i++)
            {
                packs[i].Init(this, i);
                packs[i].Held.SetGrabbable(false);
            }
        }

        public int InPlaceCount
        {
            get { int n = 0; foreach (RackPack p in packs) if (p.InRack) n++; return n; }
        }

        /// <summary>꽂힌 팩 중 최대 count개를 around 근처 바닥에 떨어뜨리고, 떨어뜨린 팩을 돌려줍니다.</summary>
        public List<RackPack> Scatter(Vector3 around, int count, IRandomSource random)
        {
            var inRack = new List<RackPack>();
            foreach (RackPack p in packs) if (p.InRack) inRack.Add(p);
            var scattered = new List<RackPack>();
            for (int i = 0; i < count && inRack.Count > 0; i++)
            {
                RackPack p = inRack[random.Range(0, inRack.Count)];
                inRack.Remove(p);
                p.Slot = -1;
                // 바닥에 눕힘: 손님 자리에서 진열대 반대쪽(-Z, 계산대 쪽) 반원, 반경 0.25~0.55m, 가게 벽 안쪽
                // (진열대 아래로 들어가 안 보이는 일이 없게)
                float angle = 100f + (float)(random.NextDouble() * 160.0);
                float dist = 0.25f + (float)random.NextDouble() * 0.3f;
                Vector3 pos = around + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist;
                pos = ClampInsideShop(pos);
                pos.y = 0.006f;
                Quaternion rot = Quaternion.Euler(90f, (float)(random.NextDouble() * 360.0), 0f);
                p.Held.PlaceAt(pos, rot);
                p.Held.SetGrabbable(true);
                scattered.Add(p);
            }
            return scattered;
        }

        /// <summary>가장 가까운 빈 칸이 놓은 위치에서 snapDistance 안이면 꽂습니다.</summary>
        public bool TryReturn(RackPack pack)
        {
            int best = -1;
            float bestSqr = snapDistance * snapDistance;
            for (int i = 0; i < slots.Length; i++)
            {
                if (Occupied(i)) continue;
                float d = (slots[i].position - pack.transform.position).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; best = i; }
            }
            if (best < 0) return false;
            Seat(pack, best);
            return true;
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

        bool Occupied(int slot)
        {
            foreach (RackPack p in packs) if (p.Slot == slot) return true;
            return false;
        }

        void Seat(RackPack pack, int slot)
        {
            pack.Slot = slot;
            pack.Held.PlaceAt(slots[slot].position, slots[slot].rotation);
            pack.Held.SetGrabbable(false);
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
