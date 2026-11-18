using System;
using System.Collections.Generic;
using CardShop.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 일시정지 메뉴에 [안내 다시 보기] · [안내 건너뛰기]를 덧붙입니다 (튜토리얼_설계 7장, 확정 결정).
    /// 씬을 다시 빌드하지 않도록 실행 중에 [이어서 하기] 버튼을 복제하고, 메뉴 판을 세로로 늘려 버튼을 다시 줄 세웁니다
    /// (1px = 1mm, 버튼 높이 62mm · 간격 30mm 이상). PauseMenu 코드는 고치지 않습니다.
    /// 건너뛰기는 실수로 누르지 않게 3초 안에 한 번 더 눌러야 합니다.
    /// </summary>
    public sealed class PauseMenuTutorialButtons : MonoBehaviour
    {
        [Tooltip("늘린 메뉴 판 높이 (mm)")]
        [SerializeField] float menuHeight = 880f;
        [Tooltip("버튼 줄의 맨 위 (판 높이 비율)")]
        [SerializeField] float listTop = 0.72f;
        [Tooltip("버튼 하나의 높이 (판 높이 비율)")]
        [SerializeField] float buttonHeight = 0.07f;
        [Tooltip("버튼 사이 간격까지 포함한 한 칸 (판 높이 비율)")]
        [SerializeField] float buttonStep = 0.105f;
        [SerializeField] float confirmSeconds = 3f;

        const string SkipLabel = "안내 건너뛰기";
        const string SkipConfirmLabel = "한 번 더 누르면 건너뛰어요";

        PauseMenu menu;
        Button replay;
        Button skip;
        TMP_Text skipText;
        Action onSkip;
        Action onReplay;
        Func<bool> available;
        float skipArmedUntil = -1f;
        readonly List<Button> order = new List<Button>();

        /// <param name="isAvailable">튜토리얼이 아직 진행 중인가 (아니면 두 버튼을 숨김)</param>
        public void Configure(PauseMenu pauseMenu, Action skipAll, Action replayLast, Func<bool> isAvailable)
        {
            menu = pauseMenu;
            onSkip = skipAll;
            onReplay = replayLast;
            available = isAvailable;
            if (menu == null || menu.MainPanel == null || menu.ResumeButton == null || menu.Root == null) return;

            replay = Clone("Button_TutorialReplay", "안내 다시 보기");
            skip = Clone("Button_TutorialSkip", SkipLabel);
            skipText = skip.GetComponentInChildren<TMP_Text>(true);
            replay.onClick.AddListener(OnReplay);
            skip.onClick.AddListener(OnSkip);

            // 원래 버튼 순서(위→아래)에 튜토리얼 두 개를 [이어서 하기] 바로 아래 끼움
            var originals = new List<Button>(menu.MainPanel.GetComponentsInChildren<Button>(true));
            originals.Remove(replay);
            originals.Remove(skip);
            originals.Sort((a, b) => ((RectTransform)b.transform).anchorMin.y.CompareTo(((RectTransform)a.transform).anchorMin.y));
            order.Clear();
            foreach (Button b in originals)
            {
                order.Add(b);
                if (b == menu.ResumeButton) { order.Add(replay); order.Add(skip); }
            }
            menu.Root.sizeDelta = new Vector2(menu.Root.sizeDelta.x, menuHeight);
        }

        Button Clone(string name, string label)
        {
            GameObject go = Instantiate(menu.ResumeButton.gameObject, menu.MainPanel.transform, false);
            go.name = name;
            var text = go.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;
            var button = go.GetComponent<Button>();
            button.onClick.RemoveAllListeners(); // 복제본에 남은 런타임 연결은 없지만 안전하게
            return button;
        }

        // 메뉴가 보일 때마다: 보이는 버튼만 위에서부터 줄 세움 (디버그 버튼은 출시 빌드에서 숨겨짐)
        void LateUpdate()
        {
            if (menu == null || menu.Root == null || !menu.Root.gameObject.activeInHierarchy) return;
            bool on = available == null || available();
            if (replay.gameObject.activeSelf != on) replay.gameObject.SetActive(on);
            if (skip.gameObject.activeSelf != on) skip.gameObject.SetActive(on);
            if (skipArmedUntil > 0f && Time.unscaledTime > skipArmedUntil)
            {
                skipArmedUntil = -1f;
                if (skipText != null) skipText.text = SkipLabel;
            }

            int row = 0;
            foreach (Button b in order)
            {
                if (b == null || !b.gameObject.activeSelf) continue;
                var rt = (RectTransform)b.transform;
                float max = listTop - row * buttonStep;
                Vector2 min = new Vector2(rt.anchorMin.x, max - buttonHeight);
                if (rt.anchorMin != min || !Mathf.Approximately(rt.anchorMax.y, max))
                {
                    rt.anchorMin = min;
                    rt.anchorMax = new Vector2(rt.anchorMax.x, max);
                }
                row++;
            }
        }

        void OnReplay() => onReplay?.Invoke();

        void OnSkip()
        {
            if (Time.unscaledTime > skipArmedUntil)
            {
                skipArmedUntil = Time.unscaledTime + confirmSeconds;
                if (skipText != null) skipText.text = SkipConfirmLabel;
                return;
            }
            skipArmedUntil = -1f;
            if (skipText != null) skipText.text = SkipLabel;
            onSkip?.Invoke();
        }
    }
}
