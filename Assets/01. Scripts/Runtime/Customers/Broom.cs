using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Customers
{
    /// <summary>
    /// 도둑 잡는 빗자루 (카드도둑_설계 2장). 잡기 · 놓기(놓으면 그 자리에 섬)는 같은 오브젝트의 HeldItem이 맡고,
    /// 이 컴포넌트는 빗자루 머리 위치와 휘두르는 속도만 잽니다. 맞았는지는 ThiefDirector가 ThiefRules.IsHit으로 판단합니다.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class Broom : MonoBehaviour
    {
        [Tooltip("빗자루 머리 가운데 (맞음 판정 기준점)")]
        [SerializeField] Transform head;

        XRGrabInteractable grab;
        Vector3 lastHead;
        bool hasLast;

        public void Configure(Transform headPoint) => head = headPoint;

        /// <summary>손에 쥐고 있는가 (쥐지 않은 빗자루는 쓰러져도 도둑을 치지 않음)</summary>
        public bool IsHeld => grab != null && grab.isSelected;
        public Vector3 HeadPosition => head != null ? head.position : transform.position;
        /// <summary>지난 프레임부터 빗자루 머리가 움직인 속도 (m/s)</summary>
        public float HeadSpeed { get; private set; }

        void Awake() => grab = GetComponent<XRGrabInteractable>();

        // 1.2m 막대를 몸 가까이 들면 플레이어 몸(CharacterController)에 걸려 이동이 막히므로 서로 부딪치지 않게 함
        void Start()
        {
            var body = FindAnyObjectByType<CharacterController>();
            if (body == null) return;
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(body, c, true);
        }

        void LateUpdate()
        {
            Vector3 now = HeadPosition;
            float dt = Time.deltaTime;
            HeadSpeed = hasLast && dt > 0f ? (now - lastHead).magnitude / dt : 0f;
            lastHead = now;
            hasLast = true;
        }
    }
}
