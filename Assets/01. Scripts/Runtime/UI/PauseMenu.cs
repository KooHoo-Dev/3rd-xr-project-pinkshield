using System.Collections;
using CardShop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 일시정지 메뉴 (시연 피드백 C1). 헤드셋을 벗거나 왼손 메뉴 버튼(에디터는 P 키)을 누르면
    /// 플레이어 눈앞(월드 고정)에 메뉴가 뜹니다.
    /// [이어서 하기] → 3·2·1 카운트다운 후 재개 / [눈높이 다시 맞추기] → 지금 자세로 1.6m 맞춤 /
    /// [처음부터 다시 시작] → 확인 후 새 판. 출시 빌드에는 디버그 패널이 없어서 눈높이 버튼을 여기에 둡니다.
    /// 어느 페이즈에서나 뜹니다 (3차 기기 피드백, GameFlow.Pause). 멈추는 것은 영업 타이머 · 손님 · 거리 행인.
    /// 이 컴포넌트는 항상 켜져 있는 오브젝트에 두고, 메뉴 화면(root)만 켜고 끕니다.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [SerializeField] GameObject root;
        [SerializeField] GameObject mainPanel;
        [SerializeField] GameObject confirmPanel;
        [SerializeField] TMP_Text countdownLabel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button restartButton;
        [Tooltip("눈높이 다시 맞추기 버튼. 비우면 버튼 없이 동작")]
        [SerializeField] Button recalibrateButton;
        [Tooltip("비우면 누를 때 씬에서 찾음 (플레이어 리그에 붙어 있음)")]
        [SerializeField] CardShop.Interaction.PlayerHeightCalibrator heightCalibrator;
        [Tooltip("디버그 패널 보이기/숨기기 버튼. Release 빌드에서는 숨김. 비워 두면 버튼 없이 동작")]
        [SerializeField] Button debugToggleButton;
        [Tooltip("켜고 끌 디버그 패널. 비워 두면 토글 버튼을 숨김")]
        [SerializeField] CardShop.DebugTools.DebugCheats debugCheats;
        [SerializeField] Button confirmYesButton;
        [SerializeField] Button confirmNoButton;
        [Tooltip("플레이어 앞 거리 (m)")]
        [SerializeField] float distance = 1.0f;
        [SerializeField] int countdownSeconds = 3;

        GameContext subscribed;
        InputAction pauseAction;
        Coroutine countdown;

        void OnEnable()
        {
            resumeButton.onClick.AddListener(OnResume);
            restartButton.onClick.AddListener(ShowConfirm);
            if (recalibrateButton != null) recalibrateButton.onClick.AddListener(OnRecalibrate);
            if (debugToggleButton != null)
            {
                bool usable = debugCheats != null && CardShop.DebugTools.DebugCheats.Available;
                debugToggleButton.gameObject.SetActive(usable);
                if (usable) debugToggleButton.onClick.AddListener(OnToggleDebug);
            }
            confirmYesButton.onClick.AddListener(OnRestartConfirmed);
            confirmNoButton.onClick.AddListener(ShowMain);

            subscribed = GameContext.Instance;
            if (subscribed != null) subscribed.Events.PausedChanged += OnPausedChanged;

            // 왼손 메뉴 버튼(Quest) / P 키(에디터 시뮬레이터)
            pauseAction = new InputAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<XRController>{LeftHand}/{Menu}");
            pauseAction.AddBinding("<Keyboard>/p");
            pauseAction.performed += OnPausePressed;
            pauseAction.Enable();

            root.SetActive(false);
        }

        void OnDisable()
        {
            resumeButton.onClick.RemoveListener(OnResume);
            restartButton.onClick.RemoveListener(ShowConfirm);
            if (recalibrateButton != null) recalibrateButton.onClick.RemoveListener(OnRecalibrate);
            if (debugToggleButton != null) debugToggleButton.onClick.RemoveListener(OnToggleDebug);
            confirmYesButton.onClick.RemoveListener(OnRestartConfirmed);
            confirmNoButton.onClick.RemoveListener(ShowMain);
            if (subscribed != null) subscribed.Events.PausedChanged -= OnPausedChanged;
            subscribed = null;
            if (pauseAction != null)
            {
                pauseAction.performed -= OnPausePressed;
                pauseAction.Dispose();
                pauseAction = null;
            }
        }

        void OnPausePressed(InputAction.CallbackContext _) => flow.Pause();

        void OnPausedChanged(bool paused)
        {
            if (paused) Show();
            else Hide();
        }

        void Show()
        {
            ShowMain();
            Place();
            root.SetActive(true);
        }

        void Hide()
        {
            if (countdown != null) { StopCoroutine(countdown); countdown = null; }
            root.SetActive(false);
        }

        // 헤드셋을 다시 쓰면 머리 위치가 바뀌어 있으므로 메뉴를 다시 눈앞에 놓습니다.
        void OnApplicationPause(bool pause)
        {
            if (!pause && root != null && root.activeSelf) Place();
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus && root != null && root.activeSelf) Place();
        }

        // 월드 고정: 시선의 수평 정면 distance, 눈높이보다 약간 아래. 머리에 붙이지 않습니다(멀미).
        void Place()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Pose p = HeadPose.InFront(cam.transform, distance, 0.1f);
            root.transform.SetPositionAndRotation(p.position, p.rotation);
        }

        void ShowMain()
        {
            UpdateDebugLabel();
            mainPanel.SetActive(true);
            confirmPanel.SetActive(false);
            countdownLabel.gameObject.SetActive(false);
        }

        void ShowConfirm()
        {
            mainPanel.SetActive(false);
            confirmPanel.SetActive(true);
        }

        void OnResume()
        {
            if (countdown == null) countdown = StartCoroutine(CountdownThenResume());
        }

        // 바로 재개하면 준비할 틈 없이 시간이 흐르므로 카운트다운을 둡니다.
        IEnumerator CountdownThenResume()
        {
            mainPanel.SetActive(false);
            countdownLabel.gameObject.SetActive(true);
            for (int n = countdownSeconds; n > 0; n--)
            {
                countdownLabel.text = n.ToString();
                yield return new WaitForSecondsRealtime(1f);
            }
            countdown = null;
            flow.Resume(); // PausedChanged(false) → Hide
        }

        void OnRestartConfirmed() => flow.RestartGame();

        // 디버그 패널은 계산대 오른쪽 고정 자리에 뜹니다 (Development 빌드 · 에디터에서만)
        void OnToggleDebug()
        {
            if (debugCheats == null) return;
            debugCheats.PanelVisible = !debugCheats.PanelVisible;
            UpdateDebugLabel();
        }

        void UpdateDebugLabel()
        {
            if (debugToggleButton == null || debugCheats == null) return;
            var label = debugToggleButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = debugCheats.PanelVisible ? "디버그 패널 숨기기" : "디버그 패널 보이기";
        }

        // 지금 자세 그대로 바로 맞춥니다(메뉴를 보고 있으니 고개는 대체로 수평). 눈높이가 바뀌었으니 메뉴도 다시 눈앞에 놓습니다.
        void OnRecalibrate()
        {
            if (heightCalibrator == null) heightCalibrator = FindAnyObjectByType<CardShop.Interaction.PlayerHeightCalibrator>();
            if (heightCalibrator == null) return;
            heightCalibrator.Calibrate();
            Place();
            if (subscribed != null) subscribed.Events.RaiseNotice($"눈높이를 {heightCalibrator.TargetEyeHeight:0.0#}m로 맞췄어요.");
        }
    }
}
