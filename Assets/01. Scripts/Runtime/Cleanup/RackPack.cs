using System;
using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 판매 진열대 위 팩 꽂이에 꽂혀 있는 장식용 팩 (흐트러진 팩 청소). 손님이 흐트러뜨리면 바닥에 떨어지고 잡을 수 있게 되며,
    /// 꽂이의 빈 칸 근처에서 놓으면 PackRack이 그 칸에 꽂습니다.
    /// </summary>
    [RequireComponent(typeof(HeldItem))]
    public sealed class RackPack : MonoBehaviour
    {
        PackRack rack;

        public HeldItem Held { get; private set; }
        /// <summary>꽂힌 칸 번호. -1이면 흐트러져 있음</summary>
        public int Slot { get; internal set; } = -1;
        public bool InRack => Slot >= 0;
        /// <summary>꽂이에 다시 꽂힘</summary>
        public event Action<RackPack> Returned;

        public void Init(PackRack owner, int slot)
        {
            rack = owner;
            Slot = slot;
            Held = GetComponent<HeldItem>();
            Held.Released += OnReleased;
        }

        void OnDestroy()
        {
            if (Held != null) Held.Released -= OnReleased;
        }

        void OnReleased(HeldItem _)
        {
            if (!InRack && rack != null && rack.TryReturn(this)) Returned?.Invoke(this);
        }
    }
}
