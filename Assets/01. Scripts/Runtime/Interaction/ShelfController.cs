using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>
    /// 팩 선반. 하루가 시작될 때마다 전체를 다시 깔고(설계서 Q5), 단서 비율은 ShelfGenerator가 정합니다.
    /// 뒤집힘 단서 팩은 뒷면이 위로 놓입니다. 산 자리는 그날 비워 둡니다.
    /// </summary>
    public sealed class ShelfController : MonoBehaviour
    {
        [SerializeField] ShelfSlotSocket[] slots;
        [SerializeField] PackView packPrefab;
        [SerializeField] PackOpener opener;

        readonly List<PackView> packs = new List<PackView>();
        GameContext context;

        public void Configure(ShelfSlotSocket[] shelfSlots, PackView prefab, PackOpener packOpener)
        {
            slots = shelfSlots;
            packPrefab = prefab;
            opener = packOpener;
        }

        void OnEnable()
        {
            context = GameContext.Instance;
            if (context == null) return;
            context.Events.DayStarted += OnDayStarted;
        }

        void OnDisable()
        {
            if (context != null) context.Events.DayStarted -= OnDayStarted;
        }

        void Start() => Restock();

        void OnDayStarted(int day) => Restock();

        public void Restock()
        {
            if (context == null) return;
            foreach (PackView p in packs)
                if (p != null) Destroy(p.gameObject);
            packs.Clear();

            PackClue[] clues = ShelfGenerator.Generate(slots.Length, context.PackOdds, context.Random);
            for (int i = 0; i < slots.Length; i++)
                Spawn(slots[i], clues[i]);
        }

        void Spawn(ShelfSlotSocket slot, PackClue clue)
        {
            bool flipped = (clue & PackClue.FlippedBack) != 0;
            Transform attach = slot.AttachFor(flipped);
            PackView pack = Instantiate(packPrefab, attach.position, attach.rotation);
            pack.Init(clue, false, this);
            pack.HomeSlot = slot;
            pack.OpenRequested += opener.TryOpen;
            packs.Add(pack);
            Insert(slot, pack);
        }

        /// <summary>선반 밖에 놓인 팩을 원래 칸(차 있으면 빈 칸)으로 돌려놓습니다. 방향은 유지.</summary>
        public void Return(PackView pack)
        {
            ShelfSlotSocket slot = pack.HomeSlot != null && !pack.HomeSlot.hasSelection ? pack.HomeSlot : FindEmptySlot();
            if (slot == null) return;
            Transform attach = slot.AttachFor(pack.IsFaceDown);
            pack.transform.SetPositionAndRotation(attach.position, attach.rotation);
            pack.HomeSlot = slot;
            Insert(slot, pack);
        }

        ShelfSlotSocket FindEmptySlot()
        {
            foreach (ShelfSlotSocket s in slots)
                if (!s.hasSelection) return s;
            return null;
        }

        static void Insert(ShelfSlotSocket slot, PackView pack)
        {
            if (slot.interactionManager != null)
                slot.interactionManager.SelectEnter((IXRSelectInteractor)slot, (IXRSelectInteractable)pack.Grab);
        }
    }
}
