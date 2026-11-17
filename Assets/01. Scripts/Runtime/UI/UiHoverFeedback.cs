using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 월드 UI 버튼에 레이가 닿았을 때의 피드백 (시연 피드백 C3): 살짝 커지고, 닿은 손에 약한 진동.
    /// 색은 Button의 ColorBlock(씬 빌더에서 지정)이 담당합니다. 버튼에 붙여 쓰세요.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public sealed class UiHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler
    {
        [SerializeField] float hoverScale = 1.06f;
        [SerializeField] float hapticAmplitude = 0.12f;
        [SerializeField] float hapticSeconds = 0.02f;

        Vector3 baseScale;
        bool hovered;

        void Awake() => baseScale = transform.localScale;

        void OnDisable()
        {
            transform.localScale = baseScale;
            hovered = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hovered) return;
            hovered = true;
            transform.localScale = baseScale * hoverScale;
            if (eventData is TrackedDeviceEventData tracked && tracked.interactor is XRBaseInputInteractor hand)
                hand.SendHapticImpulse(hapticAmplitude, hapticSeconds);
        }

        // BUG-005: 누른 버튼이 EventSystem의 '선택' 상태로 남으면, Selectable은 선택이 하이라이트보다 우선이라
        // 그 버튼에 다시 레이를 대도 색이 바뀌지 않습니다. 누른 직후 선택을 풀어 줍니다.
        public void OnPointerUp(PointerEventData eventData)
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            transform.localScale = baseScale;
        }
    }
}
