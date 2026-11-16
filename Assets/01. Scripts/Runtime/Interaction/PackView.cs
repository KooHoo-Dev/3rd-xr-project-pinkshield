using System;
using System.Collections;
using CardShop.Cards;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>개봉 요청을 보내는 방식(설계서 7-2). 트리거 개봉 ↔ 찢기 제스처를 컴포넌트 교체로 바꿉니다.</summary>
    public interface IPackOpenMethod
    {
        event Action Requested;
    }

    /// <summary>
    /// 카드팩 1개. 단서(Clue)는 생성 시 고정되어 확률에만 쓰이고,
    /// 앞/뒤 방향(IsFaceDown)은 플레이어가 바꿀 수 있는 표시 상태입니다 (설계서 7-6, DEC-001).
    /// 앞면 = 로컬 -Z 방향 (카드와 같은 규칙).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class PackView : MonoBehaviour
    {
        [Tooltip("윗면 접힘 단서일 때 회전시킬 경첩 (접힌 부분의 부모)")]
        [SerializeField] Transform foldHinge;
        [SerializeField] float foldAngle = 55f;
        [Tooltip("선반 밖에 놓인 뒤 선반으로 돌아가기까지 시간(초)")]
        [SerializeField] float returnDelay = 3f;

        XRGrabInteractable grab;
        Coroutine returnRoutine;

        public PackClue Clue { get; private set; }
        public bool IsGift { get; private set; }
        public ShelfController Shelf { get; private set; }
        public ShelfSlotSocket HomeSlot { get; set; }
        public XRGrabInteractable Grab => grab;

        public bool IsFaceDown => Vector3.Dot(-transform.forward, Vector3.up) < 0f;

        /// <summary>개봉 요청 (트리거 등). ShelfController 또는 생성한 쪽이 PackOpener로 연결합니다.</summary>
        public event Action<PackView> OpenRequested;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            foreach (IPackOpenMethod method in GetComponents<IPackOpenMethod>())
                method.Requested += () => OpenRequested?.Invoke(this);
        }

        void OnEnable() => grab.selectExited.AddListener(OnSelectExited);
        void OnDisable() => grab.selectExited.RemoveListener(OnSelectExited);

        public void Init(PackClue clue, bool isGift, ShelfController shelf)
        {
            Clue = clue;
            IsGift = isGift;
            Shelf = shelf;
            if (foldHinge != null)
                foldHinge.localRotation = (clue & PackClue.FoldedTop) != 0 ? Quaternion.Euler(-foldAngle, 0f, 0f) : Quaternion.identity;
        }

        /// <summary>개봉되어 사라짐. 손에 들려 있으면 놓게 한 뒤 제거합니다.</summary>
        public void Consume()
        {
            if (grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            Destroy(gameObject);
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor || Shelf == null) return;
            if (returnRoutine != null) StopCoroutine(returnRoutine);
            returnRoutine = StartCoroutine(ReturnLater());
        }

        IEnumerator ReturnLater()
        {
            yield return new WaitForSeconds(returnDelay);
            returnRoutine = null;
            if (!grab.isSelected && Shelf != null) Shelf.Return(this);
        }
    }

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
