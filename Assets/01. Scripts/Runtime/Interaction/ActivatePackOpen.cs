using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Interaction
{
    // MonoBehaviour는 클래스 이름과 같은 파일에 있어야 프리팹에 저장됩니다 (BUG-001).
    /// <summary>팩을 든 채 트리거를 당기면 개봉 (찢기 제스처의 대체안, 리스크 문서).</summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class ActivatePackOpen : MonoBehaviour, IPackOpenMethod
    {
        public event Action Requested;
        XRGrabInteractable grab;

        void Awake() => grab = GetComponent<XRGrabInteractable>();
        void OnEnable() => grab.activated.AddListener(OnActivated);
        void OnDisable() => grab.activated.RemoveListener(OnActivated);
        void OnActivated(ActivateEventArgs _) => Requested?.Invoke();
    }
}
