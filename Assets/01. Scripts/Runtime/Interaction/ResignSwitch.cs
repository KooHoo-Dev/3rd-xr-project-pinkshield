using CardShop.Core;
using CardShop.Core.Phases;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CardShop.Interaction
{
    /// <summary>
    /// 무한 모드 그만두기 스위치 (무한모드_설계 J4 Q4): 집 벽의 [불 끄고 자기] 스위치 옆에 빨간 [사직서] 스위치.
    /// 무한 모드일 때 집에서만 보이고, 실수로 끝나지 않게 5초 안에 두 번 눌러야 무한 모드 결과 판으로 갑니다.
    /// 씬을 다시 빌드하지 않도록 실행 중에 잠자기 스위치(PokeButton, 연결된 함수 "Sleep")를 복제해 만들고, 복제된 잠자기 연결은 끕니다.
    /// 설계서 추천(사직서 봉투를 손으로 제출함에)보다 단순한 형태: 봉투 · 제출함 자리를 확인할 수 없어 같은 벽 스위치로 둠 (아침 검토).
    /// </summary>
    public sealed class ResignSwitch : PhaseListener
    {
        const float ConfirmSeconds = 5f;

        [Tooltip("잠자기 스위치에서 옆으로 떨어진 거리 (스위치 로컬 X, m)")]
        [SerializeField] float sideOffset = 0.3f;

        PokeButton button;
        TMP_Text label;
        float armedUntil = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (GameContext.Instance == null || FindAnyObjectByType<ResignSwitch>() != null) return;
            new GameObject("ResignSwitch").AddComponent<ResignSwitch>();
        }

        protected override void Start()
        {
            PokeButton sleep = FindSleepButton();
            if (sleep != null)
            {
                GameObject go = Instantiate(sleep.gameObject, sleep.transform.parent, true);
                go.name = "Button_Resign";
                go.transform.position = sleep.transform.position + sleep.transform.right * sideOffset;
                button = go.GetComponent<PokeButton>();
                for (int i = 0; i < button.pressed.GetPersistentEventCount(); i++)
                    button.pressed.SetPersistentListenerState(i, UnityEventCallState.Off); // 복제된 "Sleep" 연결 끄기
                button.pressed.AddListener(OnPressed);
                label = go.GetComponentInChildren<TMP_Text>(true);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    if (r.GetComponent<TMP_Text>() == null) r.material.color = new Color(0.9f, 0.3f, 0.3f);
                SetLabel(false);
            }
            base.Start();
        }

        static PokeButton FindSleepButton()
        {
            foreach (PokeButton b in FindObjectsByType<PokeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                for (int i = 0; i < b.pressed.GetPersistentEventCount(); i++)
                    if (b.pressed.GetPersistentMethodName(i) == "Sleep") return b;
            return null;
        }

        protected override void Refresh()
        {
            if (button == null) return;
            bool show = CurrentPhase == GamePhaseId.Home && Context != null && Context.Session != null && Context.Session.Endless.Active;
            if (button.gameObject.activeSelf != show) button.gameObject.SetActive(show);
            armedUntil = -1f;
            SetLabel(false);
        }

        void Update()
        {
            if (armedUntil > 0f && Time.unscaledTime > armedUntil) { armedUntil = -1f; SetLabel(false); }
        }

        void OnPressed()
        {
            if (Flow == null || CurrentPhase != GamePhaseId.Home) return;
            if (armedUntil < 0f)
            {
                armedUntil = Time.unscaledTime + ConfirmSeconds;
                SetLabel(true);
                return;
            }
            armedUntil = -1f;
            Flow.GetPhase<HomePhase>(GamePhaseId.Home)?.Resign();
        }

        void SetLabel(bool armed)
        {
            if (label != null) label.text = armed ? "한 번 더 누르면\n근무 끝" : "사직서\n(근무 끝내기)";
        }
    }
}
