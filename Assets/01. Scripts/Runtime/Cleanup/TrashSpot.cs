using CardShop.Cards;
using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 바닥 쓰레기 (기획서 5-1: 집어서 쓰레기통에). 쓰레기 오브젝트 자체에 붙습니다(HeldItem과 함께).
    /// 바닥에 있어 손이 닿기 어려우면 레이저로 멀리서 잡아도 손으로 끌려옵니다(XRGrabInteractable.farAttachMode = Near).
    /// </summary>
    [RequireComponent(typeof(HeldItem))]
    public sealed class TrashSpot : CleanupSpot
    {
        HeldItem held;
        TrashBin bin;

        public override CleanupKinds Kind => CleanupKinds.Trash;

        public void Init(TrashBin trashBin)
        {
            bin = trashBin;
            held = GetComponent<HeldItem>();
            held.Released += OnReleased;
        }

        void OnDestroy()
        {
            if (held != null) held.Released -= OnReleased;
        }

        void OnReleased(HeldItem item)
        {
            if (IsDone || bin == null || !bin.Contains(transform.position)) return;
            item.PlaceAt(transform.position, transform.rotation);
            item.SetGrabbable(false);
            gameObject.SetActive(false); // 통 안으로 사라짐
            Complete();
        }
    }
}
