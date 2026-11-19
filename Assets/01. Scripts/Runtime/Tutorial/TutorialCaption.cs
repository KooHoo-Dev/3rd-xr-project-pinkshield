using CardShop.Cards;
using CardShop.Core;
using CardShop.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 자막 판 (빌드피드백_11-19_FIX설계 B1). 대상 옆에 붙던 말풍선 대신 눈 앞 아래쪽에 고정된 자막:
    /// ① 말하는 사람 칩 ② 대사(큰 흰 글씨) ③ 할 일(노란 굵은 글씨). 카드 · 물건에 가려지지 않게 맨 위에 그립니다.
    /// 머리를 15° 넘게 돌리면 천천히 따라옵니다 (완전 고정은 멀미). 새 안내가 뜨면 커지며 등장 + 띵 + 양손 약한 진동,
    /// 할 일을 마치면 할 일 줄이 초록 ✓로 바뀌고 잠깐 보여 준 뒤 다음 안내로 넘어갑니다.
    /// 튜토리얼 정지(GameFlow.IsHeld) 중에는 칩 옆에 "[시간 멈춤]", 풀리면 잠깐 "▶ 다시 시작!" (DEC-028).
    /// </summary>
    public sealed class TutorialCaption : MonoBehaviour
    {
        const float Distance = 1.0f;          // 눈에서 (m)
        const float PitchDown = 32f;          // 눈높이보다 아래로 (도). 손 · 물건을 가리지 않게 더 아래 (11-19 2차)
        const float FollowAngle = 15f;        // 이만큼 넘게 고개를 돌리면 따라옴 (도)
        const float FollowSharpness = 7.5f;   // 약 0.4초에 따라옴
        const float PopSeconds = 0.2f;
        const float DoneSeconds = 0.6f;       // ✓를 보여 주는 시간
        const float ResumeSeconds = 1.5f;     // "▶ 다시 시작!"을 보여 주는 시간
        const float FontSize = 0.32f;         // 11-19 2차: 0.42는 시야를 너무 덮어 줄임
        const float Width = 0.75f;
        const string Panel = "#2A1E45D0";     // 진보라, 뒤가 살짝 비침 (UiTheme A)
        const string Chip = "#FF8CBF";        // 분홍 (UiTheme.Accent)
        const string Task = "#FFD84D";
        const string Done = "#7CF29A";

        TextMeshPro text;
        TutorialSpeaker speaker;
        string body, task;
        float yaw, shownFor, doneUntil = -1f;
        bool following, held;
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
            // 맨 위에 그리기: 깊이 검사를 끄고 투명 물체보다 나중에 (TMP SDF 셰이더의 ZTest 속성)
            Material m = c.text.fontMaterial;
            m.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
            m.renderQueue = (int)RenderQueue.Overlay;
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

        /// <summary>가만히 있을 때: 다시 커지며 진동 (할 일 줄은 이미 보이므로 글은 그대로)</summary>
        public void Nudge()
        {
            if (!gameObject.activeSelf) return;
            shownFor = 0f;
            Buzz();
        }

        public void Hide(bool now = false)
        {
            if (now) { doneUntil = -1f; afterDone = null; }
            else if (Defer(() => Hide())) return;
            gameObject.SetActive(false);
        }

        /// <summary>지금 머리 정면으로 바로 놓음 (등장 · 헤드셋을 다시 쓴 뒤)</summary>
        public void Reposition()
        {
            Transform head = Head();
            if (head == null) return;
            yaw = head.eulerAngles.y;
            following = false;
            Place(head);
        }

        bool Defer(System.Action action)
        {
            if (doneUntil < 0f) return false;
            afterDone = action;
            return true;
        }

        void Redraw(bool done)
        {
            string who = speaker == TutorialSpeaker.Boss ? "사장님" : "혼잣말";
            string bodyColor = speaker == TutorialSpeaker.Boss ? "#FFFFFF" : "#D1DBFF";
            string state = held ? $"   <color={Task}>[시간 멈춤] 따라 해 보세요</color>"
                         : resumedUntil >= 0f ? $"   <color={Done}>▶ 다시 시작!</color>" : "";
            string s = $"<mark={Panel} padding=\"30,30,16,16\"><size=55%><color={Chip}><b>● {who}</b></color><b>{state}</b></size>\n" +
                       $"<color={bodyColor}>{body}</color>";
            if (!string.IsNullOrEmpty(task))
                s += done ? $"\n<size=85%><color={Done}><b>✓ 완료!</b></color></size>"
                          : $"\n<size=85%><color={Task}><b>▶ {task}</b></color></size>";
            text.text = s + "</mark>";
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
            float delta = Mathf.Abs(Mathf.DeltaAngle(yaw, head.eulerAngles.y));
            if (delta > FollowAngle) following = true;
            if (following)
            {
                yaw = Mathf.LerpAngle(yaw, head.eulerAngles.y, 1f - Mathf.Exp(-FollowSharpness * dt));
                if (delta < 1f) following = false;
            }
            Place(head);
        }

        // 눈에서 yaw 방향으로 Distance, PitchDown만큼 아래. TMP 글자는 -Z 쪽에서 읽히므로 forward = 눈 → 자막
        void Place(Transform head)
        {
            Vector3 dir = Quaternion.Euler(PitchDown, yaw, 0f) * Vector3.forward;
            transform.SetPositionAndRotation(head.position + dir * Distance, Quaternion.LookRotation(dir, Vector3.up));
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

        static void Buzz()
        {
            foreach (XRBaseInputInteractor hand in FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None))
                hand.SendHapticImpulse(0.25f, 0.06f);
        }

        static Transform Head()
        {
            Camera cam = Camera.main;
            return cam != null ? cam.transform : null;
        }
    }
}
