using CardShop.Cards;
using CardShop.Core;
using CardShop.Presentation;
using CardShop.UI;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 자막 판 (빌드피드백_11-19_FIX설계 B1). 대상 옆에 붙던 말풍선 대신 눈 앞 아래쪽에 고정된 자막:
    /// ① 말하는 사람 이름표 ② 대사 ③ 할 일(진한 분홍). 종이 말풍선(<see cref="PaperBubble"/>: 사장님 노랑 실선 · 혼잣말 하늘색 점선, UI_개편_설계 9번 판).
    /// 카드 · 물건에 가려지지 않게 맨 위에 그립니다.
    /// 자리는 퇴근 · 아침 혼잣말처럼 정면에 한 번 놓고 고정, 시야 밖이 1초 이어지거나 멀리 걸어가면 다시 정면에 (<see cref="CaptionPlacement"/>, 11-20).
    /// 새 안내가 뜨면 커지며 등장 + 띵 + 양손 약한 진동,
    /// 할 일을 마치면 할 일 줄이 초록 ✓로 바뀌고 잠깐 보여 준 뒤 다음 안내로 넘어갑니다.
    /// 튜토리얼 정지(GameFlow.IsHeld) 중에는 칩 옆에 "[시간 멈춤]", 풀리면 잠깐 "▶ 다시 시작!" (DEC-028).
    /// </summary>
    public sealed class TutorialCaption : MonoBehaviour
    {
        const float PopSeconds = 0.2f;
        const float DoneSeconds = 0.6f;       // ✓를 보여 주는 시간
        const float ResumeSeconds = 1.5f;     // "▶ 다시 시작!"을 보여 주는 시간
        const float FontSize = 0.32f;         // 11-19 2차: 0.42는 시야를 너무 덮어 줄임
        const float Width = 0.75f;
        const int SortBase = 100;             // 다른 3D 글자 판(알림 쪽지 등)보다 위에
        // 종이 위에서 읽히는 진한 색 (UiSkin.PaperColorFor와 같은 계열)
        const string Task = "#ED5C99";        // 할 일 = 진한 분홍 (UiTheme.Stamp)
        const string Done = "#2FA37C";        // 완료 = 진한 초록 (UiTheme.MoneyPlus)
        const string Hold = "#C23A47";        // 시간 멈춤 = 빨강 (UiTheme.Danger)

        TextMeshPro text;
        PaperBubble paper;
        TutorialSpeaker speaker;
        string body, task;
        float shownFor, doneUntil = -1f;
        float outOfViewFor;       // 시야 밖이 이어진 시간 (초)
        Vector3 placedFrom;       // 놓을 때의 머리 위치
        bool held;
        float resumedUntil = -1f;
        GameFlow flow;
        System.Action afterDone; // ✓ 보여 주는 동안 들어온 다음 Show / Hide

        public static TutorialCaption Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("TutorialCaption", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<TutorialCaption>();
            c.text = go.AddComponent<TextMeshPro>();
            if (font != null) c.text.font = font;
            c.text.fontSize = FontSize;
            c.text.alignment = TextAlignmentOptions.Center;
            c.text.textWrappingMode = TextWrappingModes.Normal;
            c.text.richText = true;
            ((RectTransform)go.transform).sizeDelta = new Vector2(Width, 0.2f);
            // 종이 말풍선 + 이름표 (UI_개편_설계 9번 판). 맨 위에 그리기: 글자 · 판 · 이름표 모두 깊이 검사를 끄고 투명 물체보다 나중에
            c.paper = PaperBubble.Attach(c.text, new Vector2(0.035f, 0.022f), 0.02f, SortBase);
            c.paper.DrawOnTop();
            go.SetActive(false);
            return c;
        }

        /// <param name="taskLine">할 일 한 줄 (버튼 이름 포함). 비우면 할 일 줄 없음</param>
        public void Show(TutorialSpeaker who, string message, string taskLine)
        {
            if (Defer(() => Show(who, message, taskLine))) return;
            speaker = who;
            body = message;
            task = taskLine;
            Redraw(false);
            shownFor = 0f;
            gameObject.SetActive(true);
            Reposition();
            Sfx.Play(SoundId.TutorialDing);
            Buzz();
        }

        /// <summary>같은 안내의 대사만 바꿈 (엉뚱한 물건을 잡았을 때 등)</summary>
        public void SetBody(string message)
        {
            body = message;
            if (gameObject.activeSelf && doneUntil < 0f) Redraw(false);
        }

        /// <summary>할 일을 마침: 할 일 줄을 초록 ✓로 바꾸고 잠깐 보여 줌. 그동안 온 Show / Hide는 끝난 뒤 처리.</summary>
        public void Complete()
        {
            if (!gameObject.activeSelf || string.IsNullOrEmpty(task)) return;
            Redraw(true);
            doneUntil = Time.unscaledTime + DoneSeconds;
        }

        /// <summary>가만히 있을 때: 지금 보는 정면으로 다시 놓고 커지며 진동 (자막을 놓쳐서 멈춰 있을 수 있음. 글은 그대로)</summary>
        public void Nudge()
        {
            if (!gameObject.activeSelf) return;
            Reposition();
            shownFor = 0f;
            Buzz();
        }

        public void Hide(bool now = false)
        {
            if (now) { doneUntil = -1f; afterDone = null; }
            else if (Defer(() => Hide())) return;
            gameObject.SetActive(false);
        }

        /// <summary>지금 보는 정면에 놓고 고정 (새 안내 · 가만히 있을 때 · 시야 밖 · 헤드셋을 다시 쓴 뒤)</summary>
        public void Reposition()
        {
            Transform head = Head();
            if (head == null) return;
            Pose p = CaptionPlacement.Front(head.position, head.forward);
            transform.SetPositionAndRotation(p.position, p.rotation);
            placedFrom = head.position;
            outOfViewFor = 0f;
        }

        bool Defer(System.Action action)
        {
            if (doneUntil < 0f) return false;
            afterDone = action;
            return true;
        }

        void Redraw(bool done)
        {
            // 말하는 사람은 이름표가 보여 주므로, 첫 줄은 정지 상태가 있을 때만
            string state = held ? $"<size=60%><color={Hold}>[시간 멈춤] 따라 해 보세요</color></size>\n"
                         : resumedUntil >= 0f ? $"<size=60%><color={Done}>▶ 다시 시작!</color></size>\n" : "";
            string s = state + body;
            if (!string.IsNullOrEmpty(task))
                s += done ? $"\n<size=85%><color={Done}>✓ 완료!</color></size>"
                          : $"\n<size=85%><color={Task}>▶ {task}</color></size>";
            paper.Show(speaker == TutorialSpeaker.Boss ? BubbleStyle.Boss : BubbleStyle.Self, s);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            WatchHold();
            if (doneUntil >= 0f && Time.unscaledTime >= doneUntil)
            {
                doneUntil = -1f;
                System.Action next = afterDone;
                afterDone = null;
                if (next != null) { next(); if (!gameObject.activeSelf) return; }
                else Redraw(false);
            }

            shownFor += dt;
            float t = Mathf.Clamp01(shownFor / PopSeconds);
            transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, 1f - (1f - t) * (1f - t));

            Transform head = Head();
            if (head == null) return;
            // 고정해 두다가, 시야 밖이 잠시 이어지거나 멀리 걸어가면(페이즈 이동 포함) 정면에 다시 놓고 다시 커지며 등장
            bool outOfView = CaptionPlacement.IsOutOfView(head.position, head.forward, transform.position);
            outOfViewFor = outOfView ? outOfViewFor + dt : 0f;
            if (outOfViewFor >= CaptionPlacement.OutOfViewSeconds || CaptionPlacement.WalkedAway(placedFrom, head.position))
            {
                Reposition();
                shownFor = 0f;
            }
        }

        // 정지가 바뀌면 칩 옆 상태 글자를 다시 그림
        void WatchHold()
        {
            if (flow == null && GameContext.Instance != null) flow = GameContext.Instance.GetComponent<GameFlow>();
            bool now = flow != null && flow.IsHeld;
            bool expired = resumedUntil >= 0f && Time.unscaledTime >= resumedUntil;
            if (now == held && !expired) return;
            if (expired) resumedUntil = -1f;
            if (now != held) { held = now; resumedUntil = now ? -1f : Time.unscaledTime + ResumeSeconds; }
            Redraw(doneUntil >= 0f);
        }

        static void Buzz() => Interaction.Haptics.BothHands(0.25f, 0.06f);

        static Transform Head()
        {
            Camera cam = Camera.main;
            return cam != null ? cam.transform : null;
        }
    }
}
