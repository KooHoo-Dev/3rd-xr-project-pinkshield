using UnityEngine;
using UnityEngine.InputSystem;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 플레이어가 아무것도 하지 않은 시간을 잽니다 (일시정지와 상관없이 실제 시간).
    /// XR 컨트롤러 버튼 · 트리거 · 그립을 누르거나 스틱을 0.2 넘게 밀면 0으로 돌아갑니다.
    /// 입력은 PauseMenu처럼 코드로 만든 InputAction (XRController 용도 바인딩: OpenXR 컨트롤러 레이아웃이 모두 채움).
    /// XRI 잡기 · 호버 같은 상호작용은 TutorialDirector가 <see cref="Restart"/>로 알려 줍니다.
    /// </summary>
    public sealed class IdleHint : MonoBehaviour
    {
        [Tooltip("스틱을 이만큼 넘게 밀면 입력으로 봄")]
        [SerializeField, Range(0f, 1f)] float stickDeadZone = 0.2f;

        InputAction buttonAction;
        InputAction stickAction;

        /// <summary>마지막 입력 뒤 지난 시간 (초, unscaled).</summary>
        public float IdleSeconds { get; private set; }

        public bool IsIdle(float seconds) => IdleSeconds >= seconds;

        public void Restart() => IdleSeconds = 0f;

        void Awake()
        {
            buttonAction = new InputAction("TutorialIdleButtons", InputActionType.PassThrough);
            buttonAction.AddBinding("<XRController>/{TriggerButton}");
            buttonAction.AddBinding("<XRController>/{GripButton}");
            buttonAction.AddBinding("<XRController>/{PrimaryButton}");
            buttonAction.AddBinding("<XRController>/{SecondaryButton}");
            buttonAction.AddBinding("<XRController>/{Primary2DAxisClick}");
            buttonAction.AddBinding("<XRController>/{MenuButton}");
            buttonAction.performed += OnButton;

            stickAction = new InputAction("TutorialIdleStick", InputActionType.PassThrough, expectedControlType: "Vector2");
            stickAction.AddBinding("<XRController>/{Primary2DAxis}");
            stickAction.performed += OnStick;
        }

        void OnEnable()
        {
            IdleSeconds = 0f;
            buttonAction.Enable();
            stickAction.Enable();
        }

        void OnDisable()
        {
            buttonAction.Disable();
            stickAction.Disable();
        }

        void OnDestroy()
        {
            buttonAction.performed -= OnButton;
            stickAction.performed -= OnStick;
            buttonAction.Dispose();
            stickAction.Dispose();
        }

        void Update() => IdleSeconds += Time.unscaledDeltaTime;

        // 누를 때만 (뗄 때는 무시)
        void OnButton(InputAction.CallbackContext ctx)
        {
            if (ctx.ReadValueAsButton()) IdleSeconds = 0f;
        }

        void OnStick(InputAction.CallbackContext ctx)
        {
            if (ctx.ReadValue<Vector2>().sqrMagnitude > stickDeadZone * stickDeadZone) IdleSeconds = 0f;
        }
    }
}
