using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Interaction
{
    /// <summary>
    /// 손가락(Poke)으로 누르는 버튼. XRSimpleInteractable + XRPokeFilter와 함께 씁니다.
    /// 결제 · 상자 개봉 · 퇴근 버튼에 공통 사용 (기획서 7장 XRI Poke).
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class PokeButton : MonoBehaviour
    {
        [SerializeField] float cooldown = 0.6f;
        [Tooltip("눌렸을 때 아래로 들어가는 시각 요소 (선택)")]
        [SerializeField] Transform pressVisual;
        [SerializeField] float pressDepth = 0.01f;

        public UnityEvent pressed = new UnityEvent();

        XRSimpleInteractable interactable;
        float lastPress = -10f;
        Vector3 restPos;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (pressVisual != null) restPos = pressVisual.localPosition;
        }

        void OnEnable()
        {
            interactable.selectEntered.AddListener(OnPress);
            interactable.selectExited.AddListener(OnRelease);
        }

        void OnDisable()
        {
            interactable.selectEntered.RemoveListener(OnPress);
            interactable.selectExited.RemoveListener(OnRelease);
        }

        void OnPress(SelectEnterEventArgs _)
        {
            if (pressVisual != null) pressVisual.localPosition = restPos + Vector3.down * pressDepth;
            if (Time.unscaledTime - lastPress < cooldown) return;
            lastPress = Time.unscaledTime;
            pressed.Invoke();
        }

        void OnRelease(SelectExitEventArgs _)
        {
            if (pressVisual != null) pressVisual.localPosition = restPos;
        }
    }
}
