using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 청소 물건(쓰레기 · 걸레 · 진열대 팩)의 공통 잡기 동작. 물리 없이 잡고(XRGrabInteractable, 키네마틱),
    /// 놓으면 Released를 알립니다. 받는 쪽이 그 안에서 PlaceAt을 부르지 않으면 잠시 뒤 쉬던 자리로 돌아갑니다
    /// (ScannableItem · BarcodeScanner와 같은 방식: 바닥에 떨어져 못 줍는 일 없음).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class HeldItem : MonoBehaviour
    {
        [Tooltip("놓은 뒤 쉬던 자리로 돌아가기까지 (초)")]
        [SerializeField] float returnDelay = 1.2f;

        XRGrabInteractable grab;
        Vector3 restPos;
        Quaternion restRot;
        Coroutine returnRoutine;
        bool placed;

        public bool IsHeld => grab != null && grab.isSelected;
        /// <summary>손에서 놓음. 받는 쪽이 이 안에서 PlaceAt을 부르면 돌아가지 않습니다.</summary>
        public event Action<HeldItem> Released;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            restPos = transform.position;
            restRot = transform.rotation;
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
            StopReturn();
        }

        void OnGrabbed(SelectEnterEventArgs _) => StopReturn();

        void OnReleased(SelectExitEventArgs _)
        {
            placed = false;
            Released?.Invoke(this);
            if (!placed && isActiveAndEnabled) returnRoutine = StartCoroutine(ReturnToRest());
        }

        IEnumerator ReturnToRest()
        {
            yield return new WaitForSeconds(returnDelay);
            transform.SetPositionAndRotation(restPos, restRot);
            returnRoutine = null;
        }

        void StopReturn()
        {
            if (returnRoutine == null) return;
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        /// <summary>이 자리에 두고, 앞으로 놓쳤을 때 돌아올 자리도 여기로 바꿉니다.</summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            placed = true;
            StopReturn();
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
