using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using CardShop.Core;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 청소 물건(쓰레기 · 진열대 팩)의 공통 잡기 동작. 물리 없이 잡고(XRGrabInteractable, 키네마틱),
    /// 잡을 때 Grabbed, 놓을 때 Released를 알립니다.
    /// 받는 쪽(쓰레기통 · 팩 꽂이)이 Released 안에서 PlaceAt을 부르지 않으면, 놓은 자리 바로 아래 바닥(또는 가구 윗면)으로
    /// 툭 떨어져 그 자리에 머뭅니다 (3차 기기 피드백: 들고 다니다 내려놓는 느낌. 예전엔 처음 자리로 순간이동했음).
    /// 아래가 가게 밖이거나 받칠 곳이 없으면 마지막으로 쉬던 자리로 돌아갑니다(못 줍는 일 없음).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class HeldItem : MonoBehaviour
    {
        [Tooltip("놓은 뒤 아래로 떨어지는 시간 (초)")]
        [SerializeField] float dropSeconds = 0.25f;
        [Tooltip("놓은 자리에서 아래로 찾아보는 거리 (m)")]
        [SerializeField] float dropSearch = 3f;

        XRGrabInteractable grab;
        Vector3 restPos;
        Quaternion restRot;
        Coroutine dropRoutine;
        bool placed;

        float landHeight = -1f; // 음수 = 아직 안 정함 (Awake에서 처음 높이로)

        /// <summary>내려앉았을 때 받침면에서 물건 중심까지 높이 (m). 기본 = 처음 놓인 높이(바닥 기준)</summary>
        public float LandHeight
        {
            get => landHeight;
            set => landHeight = Mathf.Max(0.001f, value);
        }
        /// <summary>true면 내려앉을 때 눕힘(팩처럼 앞면이 위). false면 처음 자세</summary>
        public bool LandFlat { get; set; }

        public bool IsHeld => grab != null && grab.isSelected;
        /// <summary>바닥에서 주워 와야 하는 물건 (도둑이 떨군 무료 팩, 11-22 T3): 쓰레기처럼 연주황 테 + 바닥 고리 (InteractableOutline)</summary>
        public bool NeedsPickup { get; set; }

        /// <summary>손에 쥠</summary>
        public event Action<HeldItem> Grabbed;
        /// <summary>손에서 놓음. 받는 쪽이 이 안에서 PlaceAt을 부르면 떨어지지 않습니다.</summary>
        public event Action<HeldItem> Released;
        /// <summary>놓은 뒤 받침면에 내려앉음. 값 = 떨어진 높이(m) — 떨어짐 소리용 (GrabSound, 11-21 S5)</summary>
        public event Action<float> Landed;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            restPos = transform.position;
            restRot = transform.rotation;
            // 꽂이 팩처럼 다른 컴포넌트가 먼저 정했으면 그대로 둠 (Awake 순서는 정해져 있지 않음)
            if (landHeight < 0f) landHeight = Mathf.Max(0.005f, transform.position.y);
        }

        void OnEnable()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
            StopDrop();
        }

        void OnGrabbed(SelectEnterEventArgs _)
        {
            StopDrop();
            Grabbed?.Invoke(this);
        }

        void OnReleased(SelectExitEventArgs _)
        {
            placed = false;
            Released?.Invoke(this);
            if (placed || !isActiveAndEnabled) return;

            Pose landing = FindLanding();
            restPos = landing.position;
            restRot = landing.rotation;
            dropRoutine = StartCoroutine(DropTo(landing));
        }

        // 놓은 자리 바로 아래의 고정된 면(바닥 · 카운터 등). 움직이는 것(카드 · 손님 · 다른 청소 물건)은 받침으로 쓰지 않음
        Pose FindLanding()
        {
            Vector3 from = transform.position + Vector3.up * 0.02f;
            float bestDist = float.PositiveInfinity;
            Vector3 hitPoint = Vector3.zero;
            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, dropSearch, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.attachedRigidbody != null) continue;
                if (hit.distance < bestDist) { bestDist = hit.distance; hitPoint = hit.point; }
            }
            if (float.IsPositiveInfinity(bestDist)) hitPoint = new Vector3(from.x, 0f, from.z); // 콜라이더 없는 바닥이면 y=0
            hitPoint.y = FloorSurface.TopAt(hitPoint, hitPoint.y); // 콜라이더 없는 매트 · 러그 위에 (11-22 T2)

            Vector3 pos = hitPoint + Vector3.up * LandHeight;
            if (!ShopLayout.InsideShop(pos, 0.2f)) return new Pose(restPos, restRot);

            float yaw = transform.eulerAngles.y;
            Quaternion rot = LandFlat ? Quaternion.Euler(90f, yaw, 0f) : Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, -restRot.eulerAngles.y, 0f) * restRot;
            return new Pose(pos, rot);
        }

        IEnumerator DropTo(Pose landing)
        {
            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, dropSeconds))
            {
                float e = t * t; // 떨어지듯 점점 빨라짐
                transform.SetPositionAndRotation(Vector3.Lerp(fromPos, landing.position, e), Quaternion.Slerp(fromRot, landing.rotation, t));
                yield return null;
            }
            transform.SetPositionAndRotation(landing.position, landing.rotation);
            dropRoutine = null;
            Landed?.Invoke(fromPos.y - landing.position.y);
        }

        void StopDrop()
        {
            if (dropRoutine == null) return;
            StopCoroutine(dropRoutine);
            dropRoutine = null;
        }

        /// <summary>이 자리에 두고, 앞으로 받칠 곳이 없을 때 돌아올 자리도 여기로 바꿉니다.</summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            placed = true;
            StopDrop();
            restPos = position;
            restRot = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>잡을 수 있는지 켜고 끕니다. 끌 때 손에 들려 있으면 먼저 놓게 합니다.</summary>
        public void SetGrabbable(bool on)
        {
            if (grab == null) grab = GetComponent<XRGrabInteractable>();
            if (!on && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            grab.enabled = on;
        }
    }
}
