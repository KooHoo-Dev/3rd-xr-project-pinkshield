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
        [Tooltip("접힘 단서는 '찾아낸 사람만 이득'인 숨은 요소: 오른쪽 위 모서리만 살짝. 얇은 윗띠의 모서리만 꺾음. 7° → 15° → 20° → 25° (표지처럼 보이던 것을 호일 띠로 바꿈)")]
        [SerializeField] float foldAngle = 25f;
        [Tooltip("선반 밖에 놓인 뒤 선반으로 돌아가기까지 시간(초)")]
        [SerializeField] float returnDelay = 3f;

        XRGrabInteractable grab;
        Coroutine returnRoutine;
        Quaternion hingeBase = Quaternion.identity;
        bool consumed;

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
            if (foldHinge != null) hingeBase = foldHinge.localRotation; // 모서리 경첩은 대각선 방향이라 기본 회전이 있음
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
            {
                bool folded = (clue & PackClue.FoldedTop) != 0;
                foldHinge.gameObject.SetActive(folded); // 접히지 않은 팩에는 모서리 조각 자체가 없음
                foldHinge.localRotation = folded ? hingeBase * Quaternion.Euler(-foldAngle, 0f, 0f) : hingeBase;
            }
        }

        /// <summary>
        /// 개봉됨: 바로 지우지 않고 뜯김 연출(PackTearEffect)을 위해 그 자리(눈앞)에 그대로 멈춰 둡니다 (시연 피드백 A4).
        /// 손에서 놓게 하고, 다시 잡히거나 선반으로 돌아가거나 또 열리지 않게 상호작용·물리를 모두 끕니다.
        /// 오브젝트 제거는 연출 쪽이 끝날 때 합니다.
        /// </summary>
        public void DetachForReveal()
        {
            consumed = true;
            // 놓는 순간 TearGestureOpen이 눈앞 고정을 풀며 크기를 되돌리므로, 지금 모습을 기억했다가 다시 맞춥니다.
            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            Vector3 scale = transform.localScale;

            grab.throwOnDetach = false; // 놓을 때 손 속도로 튀어 나가지 않게
            if (grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            grab.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (IPackOpenMethod method in GetComponents<IPackOpenMethod>())
                if (method is Behaviour b) b.enabled = false;
            if (returnRoutine != null) { StopCoroutine(returnRoutine); returnRoutine = null; }

            transform.SetPositionAndRotation(pos, rot);
            transform.localScale = scale;
            var body = GetComponent<Rigidbody>();
            if (body != null)
            {
                // BUG-003: 스크립트로 움직일 물체는 보간을 끄고 Transform과 Rigidbody를 함께 옮깁니다.
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
                body.position = pos;
                body.rotation = rot;
            }
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (consumed || args.interactorObject is XRSocketInteractor || Shelf == null) return;
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
}
