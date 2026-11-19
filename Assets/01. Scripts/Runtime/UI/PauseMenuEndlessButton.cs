using CardShop.Core;
using CardShop.Core.Phases;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 무한 모드 일시정지 메뉴의 [근무 끝내기] (무한모드_설계 J4 Q4 보조: 책상 위 사직서 봉투를 못 찾는 사람용).
    /// 무한 모드이면서 집에 있을 때만 보입니다(그만두기는 집에서만 됨, <see cref="HomePhase.Resign"/>).
    /// 실수로 누르지 않게 3초 안에 한 번 더 눌러야 합니다. 씬을 다시 빌드하지 않도록 실행 중에 [이어서 하기] 버튼을 복제하고,
    /// 처음 보일 때 만듭니다. 줄 세우기는 <see cref="CardShop.Tutorial.PauseMenuTutorialButtons"/>가 새 버튼까지 맡습니다.
    /// </summary>
    public sealed class PauseMenuEndlessButton : PhaseListener
    {
        const float ConfirmSeconds = 3f;
        const string Label = "근무 끝내기 (사직)";
        const string ConfirmLabel = "한 번 더 누르면 사직해요";

        PauseMenu menu;
        Button button;
        TMP_Text text;
        float armedUntil = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (GameContext.Instance == null || FindAnyObjectByType<PauseMenuEndlessButton>() != null) return;
            new GameObject("PauseMenuEndlessButton").AddComponent<PauseMenuEndlessButton>();
        }

        bool Available => Context != null && Context.Session.Endless.Active && !Context.Session.Endless.Finished
            && CurrentPhase == GamePhaseId.Home;

        void LateUpdate()
        {
            if (menu == null) menu = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (menu == null || menu.Root == null || !menu.Root.gameObject.activeInHierarchy) return;

            bool on = Available;
            if (button == null)
            {
                if (!on) return; // 무한 모드 집에서 메뉴를 처음 열 때 만듦 (튜토리얼 버튼이 먼저 자리 잡은 뒤)
                Create();
            }
            if (button.gameObject.activeSelf != on) button.gameObject.SetActive(on);
            if (armedUntil > 0f && Time.unscaledTime > armedUntil) Disarm();
        }

        void Create()
        {
            GameObject go = Instantiate(menu.ResumeButton.gameObject, menu.MainPanel.transform, false);
            go.name = "Button_EndlessResign";
            text = go.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = Label;
            button = go.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }

        void OnClick()
        {
            if (Time.unscaledTime > armedUntil)
            {
                armedUntil = Time.unscaledTime + ConfirmSeconds;
                if (text != null) text.text = ConfirmLabel;
                return;
            }
            Disarm();
            if (Flow == null || !Available) return;
            Flow.Resume(); // 메뉴를 닫음 (일시정지는 페이즈가 바뀌어도 남으므로)
            Flow.GetPhase<HomePhase>(GamePhaseId.Home)?.Resign();
        }

        // 보이기 · 숨기기는 메뉴가 떠 있을 때 LateUpdate가 함. 페이즈가 바뀌면 눌러 둔 확인만 풂
        protected override void Refresh() => Disarm();

        void Disarm()
        {
            armedUntil = -1f;
            if (text != null) text.text = Label;
        }
    }
}
