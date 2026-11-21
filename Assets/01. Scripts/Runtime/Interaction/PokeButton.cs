using CardShop.Cards;
using CardShop.Presentation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Interaction
{
    /// <summary>
    /// 손가락(Poke)으로 누르는 버튼. XRSimpleInteractable + XRPokeFilter와 함께 씁니다.
    /// 결제 · 상자 개봉 · 퇴근 버튼에 공통 사용 (기획서 7장 XRI Poke).
    /// holdSeconds가 0보다 크면 그 시간 동안 눌러 둬야 실행되고, 누르는 동안 버튼 위에 둥근 게이지 + 점점 세지는 진동이 나옵니다.
    /// 퇴근 버튼은 실수로 누르면 무료 팩 · 개봉 기회가 넘어가서 0.5초 (11-21 2차 H1). 손가락 · 레이(트리거 · 그립) 모두 같음.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class PokeButton : MonoBehaviour
    {
        const float ClockOutHoldSeconds = 0.5f;

        [SerializeField] float cooldown = 0.6f;
        [Tooltip("눌렀을 때 아래로 들어가는 시각 요소 (선택)")]
        [SerializeField] Transform pressVisual;
        [SerializeField] float pressDepth = 0.01f;
        [Tooltip("이만큼 눌러 둬야 실행 (초, 0 = 닿는 순간). 퇴근 버튼은 비워 두면 스스로 0.5초")]
        [SerializeField] float holdSeconds;

        public UnityEvent pressed = new UnityEvent();

        XRSimpleInteractable interactable;
        float lastPress = -10f;
        Vector3 restPos;
        float holdStart = -1f; // 누르기 시작한 때 (-1 = 안 누름)
        string holdLabel = "꾹 누르면 실행";
        SkipHoldGauge gauge;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (pressVisual != null) restPos = pressVisual.localPosition;
            // 씬을 다시 만들지 않아도 되게: 퇴근 버튼(빌더가 TryClockOut을 연결)은 스스로 꾹 누르기
            if (holdSeconds <= 0f && Calls("TryClockOut")) { holdSeconds = ClockOutHoldSeconds; holdLabel = "꾹 누르면 퇴근"; }
        }

        bool Calls(string method)
        {
            for (int i = 0; i < pressed.GetPersistentEventCount(); i++)
                if (pressed.GetPersistentMethodName(i) == method) return true;
            return false;
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
            CancelHold();
        }

        void OnPress(SelectEnterEventArgs _)
        {
            if (pressVisual != null) pressVisual.localPosition = restPos + Vector3.down * pressDepth;
            if (Time.unscaledTime - lastPress < cooldown) return;
            if (holdSeconds > 0f) { holdStart = Time.unscaledTime; return; } // 실행은 Update에서 시간이 차면
            Fire();
        }

        void OnRelease(SelectExitEventArgs _)
        {
            if (pressVisual != null) pressVisual.localPosition = restPos;
            CancelHold(); // 다 차기 전에 떼면 취소
        }

        void Update()
        {
            if (holdStart < 0f) return;
            float p = (Time.unscaledTime - holdStart) / holdSeconds;
            if (p >= 1f) { CancelHold(); Fire(); return; }
            ShowGauge(p);
            Haptics.BothHands(Mathf.Lerp(0.1f, 0.5f, p), 0.03f); // 점점 세짐 = 눌림이 전달되는 중
        }

        void Fire()
        {
            lastPress = Time.unscaledTime;
            Sfx.Play(SoundId.UiClick, transform.position);
            pressed.Invoke();
        }

        void ShowGauge(float progress)
        {
            if (gauge == null) gauge = SkipHoldGauge.Create(transform, CardShop.UI.UiFonts.Main, holdLabel);
            Camera cam = Camera.main;
            Vector3 pos = transform.position + Vector3.up * 0.12f;
            Quaternion rot = cam != null ? Quaternion.LookRotation(pos - cam.transform.position, Vector3.up) : transform.rotation;
            gauge.Show(new Pose(pos, rot), progress);
        }

        void CancelHold()
        {
            holdStart = -1f;
            if (gauge != null) gauge.Hide();
        }
    }
}
