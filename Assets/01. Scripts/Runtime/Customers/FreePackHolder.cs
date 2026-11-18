using CardShop.Cleanup;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 계산대 위 "오늘 무료 팩"을 실제로 옮기는 손 (카드도둑_설계). 어디 있어야 하는지는 FreePackState가 정하고, 이 클래스는 옮기기만 합니다.
    /// 제자리 기억 · 진열 켜고 끄기 · 도둑 손에 붙이기 · 바닥에 떨어뜨리기 · 제자리로 돌리기.
    /// ThiefDirector가 부르고, 이 클래스는 ThiefDirector를 모릅니다.
    /// </summary>
    public sealed class FreePackHolder
    {
        readonly HeldItem pack;
        readonly GameObject display;
        readonly Transform homeParent;
        readonly Vector3 homeLocalPos;
        readonly Quaternion homeLocalRot;
        bool returning;

        /// <param name="pack">무료 팩 (잡기 + HeldItem). 만들 때 놓인 자리가 돌아올 자리</param>
        /// <param name="display">받침 + 안내 글자 묶음 (없으면 팩만 켜고 끔)</param>
        public FreePackHolder(HeldItem pack, GameObject display)
        {
            this.pack = pack;
            this.display = display;
            homeParent = pack.transform.parent;
            homeLocalPos = pack.transform.localPosition;
            homeLocalRot = pack.transform.localRotation;
        }

        /// <summary>영업 중에만 보임 (퇴근 후에는 팩 고르기에서 무료로 깜).</summary>
        public void Show(bool on)
        {
            if (display != null) display.SetActive(on);
            if (display == null || !pack.transform.IsChildOf(display.transform)) pack.gameObject.SetActive(on);
        }

        /// <summary>도둑 손에 붙임 (잡을 수 없게).</summary>
        public void AttachTo(Transform hand)
        {
            pack.SetGrabbable(false);
            pack.transform.SetParent(hand, true);
            pack.transform.localPosition = Vector3.zero;
        }

        /// <summary>도둑 발 앞 바닥에 눕혀 떨어뜨리고 잡을 수 있게 함.</summary>
        /// <param name="lyingHeight">눕혔을 때 두께의 절반 (m)</param>
        public void DropInFrontOf(Transform thief, float lyingHeight)
        {
            Vector3 feet = thief.position;
            Vector3 pos = feet + thief.forward * 0.35f;
            pos.y = feet.y + lyingHeight;
            pack.transform.SetParent(homeParent, true);
            pack.PlaceAt(pos, Quaternion.Euler(90f, thief.eulerAngles.y, 0f));
            pack.SetGrabbable(true);
        }

        /// <summary>제자리(계산대 위)로 돌림. hide면 진열도 끔(놓친 날).</summary>
        public void ReturnHome(bool hide = false)
        {
            // 잡고 있던 팩이면 잡기를 끊는 순간 Released가 오는데, 그때 HeldItem이 바닥으로 떨어뜨리지 않게 OnReleased가 제자리에 둠
            returning = true;
            pack.SetGrabbable(false);
            returning = false;
            PlaceHome();
            if (hide) Show(false);
        }

        /// <summary>HeldItem.Released에 연결. 제자리로 돌리는 중에 놓이면 그 자리에서 바로 제자리로.</summary>
        public void OnReleased(HeldItem _)
        {
            if (returning) PlaceHome();
        }

        void PlaceHome()
        {
            pack.transform.SetParent(homeParent, false);
            pack.PlaceAt(
                homeParent != null ? homeParent.TransformPoint(homeLocalPos) : homeLocalPos,
                homeParent != null ? homeParent.rotation * homeLocalRot : homeLocalRot);
        }
    }
}
