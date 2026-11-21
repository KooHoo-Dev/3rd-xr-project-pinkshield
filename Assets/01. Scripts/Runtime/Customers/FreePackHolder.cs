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

        /// <summary>지킬 팩(받침 + 금색 팩)은 영업 중에만 보임. 퇴근 후에는 ShowStandOnly + FreePackOffer.</summary>
        public void Show(bool on)
        {
            if (display != null) display.SetActive(on);
            // 팩은 받침의 자식이라도 따로 켬: 퇴근 때 ShowStandOnly가 팩만 꺼 두므로, 받침만 켜면 다음 날 팩이 계속 안 보임 (11-19 A1)
            pack.gameObject.SetActive(on);
        }

        /// <summary>제자리(받침 위) 월드 자세.</summary>
        public Pose HomePose => new Pose(
            homeParent != null ? homeParent.TransformPoint(homeLocalPos) : homeLocalPos,
            homeParent != null ? homeParent.rotation * homeLocalRot : homeLocalRot);

        /// <summary>받침 · 안내 글자만 켜고 끔 (퇴근 후: 지킨 팩 대신 실제로 열 수 있는 팩을 FreePackOffer가 올려 둠).</summary>
        public void ShowStandOnly(bool on)
        {
            if (display != null) display.SetActive(on);
            pack.gameObject.SetActive(false);
        }

        /// <summary>도둑 손에 붙임 (잡을 수 없게). 손바닥 앞쪽에 세워서 들고 있는 것처럼 보이게.</summary>
        /// <param name="body">도둑 몸 (손 뼈 축은 모델마다 달라서 몸 방향 기준으로 자세를 잡음)</param>
        public void AttachTo(Transform hand, Transform body)
        {
            pack.gameObject.SetActive(true);
            pack.SetGrabbable(false);
            Vector3 palm = hand.position + body.forward * PalmForward + Vector3.down * PalmDown;
            pack.transform.SetPositionAndRotation(palm, Quaternion.LookRotation(body.forward, Vector3.up));
            pack.transform.SetParent(hand, true);
        }

        // 손 뼈(손목 쪽)에서 손바닥 가운데까지 대략 거리 (m)
        const float PalmForward = 0.06f;
        const float PalmDown = 0.05f;

        /// <summary>도둑 발 앞 바닥에 눕혀 떨어뜨리고 잡을 수 있게 함.</summary>
        /// <param name="lyingHeight">눕혔을 때 두께의 절반 (m)</param>
        public void DropInFrontOf(Transform thief, float lyingHeight)
        {
            Vector3 feet = thief.position;
            Vector3 pos = feet + thief.forward * 0.35f;
            pos.y = FloorSurface.TopAt(pos, feet.y) + lyingHeight; // 문 앞 매트에 묻히지 않게 (11-22 T2)
            pack.NeedsPickup = true; // 치울 것처럼 연주황 테 + 바닥 고리 (11-22 T3)
            pack.gameObject.SetActive(true);
            pack.transform.SetParent(homeParent, true);
            pack.PlaceAt(pos, Quaternion.Euler(90f, thief.eulerAngles.y, 0f));
            pack.SetGrabbable(true);
        }

        /// <summary>제자리(계산대 위)로 돌림. hide면 진열도 끔(놓친 날). 아니면 팩을 켬(도둑 손에서 사라진 채로 돌아오지 않게).</summary>
        public void ReturnHome(bool hide = false)
        {
            // 잡고 있던 팩이면 잡기를 끊는 순간 Released가 오는데, 그때 HeldItem이 바닥으로 떨어뜨리지 않게 OnReleased가 제자리에 둠
            returning = true;
            pack.SetGrabbable(false);
            returning = false;
            PlaceHome();
            if (hide) Show(false);
            else pack.gameObject.SetActive(true); // 받침(display)은 건드리지 않음 — 받침이 꺼져 있으면 팩도 안 보임
        }

        /// <summary>HeldItem.Released에 연결. 제자리로 돌리는 중에 놓이면 그 자리에서 바로 제자리로.</summary>
        public void OnReleased(HeldItem _)
        {
            if (returning) PlaceHome();
        }

        void PlaceHome()
        {
            pack.NeedsPickup = false;
            pack.transform.SetParent(homeParent, false);
            Pose home = HomePose;
            pack.PlaceAt(home.position, home.rotation);
        }
    }
}
