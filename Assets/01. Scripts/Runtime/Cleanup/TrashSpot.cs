using CardShop.Cards;
using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 바닥 쓰레기 (기획서 5-1: 집어서 쓰레기통에). 쓰레기 오브젝트 자체에 붙습니다(HeldItem과 함께).
    /// 카드처럼 잡은 자리 그대로 들고 다니고(동적 어태치), 레이저로 멀리서 잡으면 레이저 끝에 매달려 옵니다.
    /// 들고 있는 동안 쓰레기통에 "여기에 버리기" 표시가 뜹니다.
    /// </summary>
    [RequireComponent(typeof(HeldItem))]
    public sealed class TrashSpot : CleanupSpot
    {
        HeldItem held;
        TrashBin bin;


        public void Init(TrashBin trashBin)
        {
            bin = trashBin;
            held = GetComponent<HeldItem>();
            held.Grabbed += OnGrabbed;
            held.Released += OnReleased;
        }

        void OnDestroy()
        {
            if (held != null)
            {
                held.Grabbed -= OnGrabbed;
                held.Released -= OnReleased;
            }
            if (bin != null) bin.EndCarry(transform);
        }

        void OnGrabbed(HeldItem _)
        {
            if (!IsDone && bin != null) bin.BeginCarry(transform);
        }

        void OnReleased(HeldItem item)
        {
            if (bin != null) bin.EndCarry(transform);
            if (IsDone || bin == null || (bin.IsPaused != null && bin.IsPaused()) || !bin.Contains(transform.position)) return;
            item.PlaceAt(transform.position, transform.rotation);
            item.SetGrabbable(false);
            gameObject.SetActive(false); // 통 안으로 사라짐
            Complete();
        }
    }
}
