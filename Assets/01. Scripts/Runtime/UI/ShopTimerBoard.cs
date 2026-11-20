using CardShop.Core;
using CardShop.Core.Phases;
using CardShop.Logic;
using CardShop.Presentation;
using CardShop.Tutorial;
using TMPro;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 가게 벽시계 (빌드피드백_11-19_FIX설계 B5 → 11-20 F6, UI 개편 C안 4장 3번). 계산대 정면(창문 위 벽)에 걸려 게임 시각을 보여 줍니다.
    /// 나무 틀 + 검은 LCD 숫자판(금색 숫자) + 아래 분홍 "마감까지 막대"(영업 시간 중 남은 비율, 줄어듦). 청소 중이면 옆에 노란 메모지로 남은 청소 수.
    /// (11-20 샘플 3번 적용: 처음 만든 밝은 종이 숫자판 → 샘플의 검은 LCD)
    /// 영업 2분 남짓 = 09:00 → 16:00, 마감 청소는 16:00부터 1초 = 1분. 30초 아래 · 청소 중에는 숫자가 빨갛게 깜빡입니다.
    /// 영업 중에만 보이고, 씬을 다시 빌드하지 않아도 되게 씬이 열리면 스스로 만들어집니다 (모양은 실행 중에 코드로 입힘).
    /// </summary>
    public sealed class ShopTimerBoard : PhaseListener
    {
        const float WarnSeconds = 30f;    // 이 아래면 빨갛게 깜빡임
        const float FrameInset = 0.035f;  // 나무 틀 두께 (m)
        const float BarWidth = 0.9f;
        const float BarHeight = 0.035f;
        const float BarY = -0.14f;

        static readonly Color WarnRed = new Color(1f, 0.35f, 0.4f);     // 검은 판 위에서 보이는 밝은 빨강
        static readonly Color Lcd = new Color(0.09f, 0.067f, 0.12f);   // #17111F
        public const string AnchorKey = "Sign/ShopClock";
        public static readonly Vector2 BoardSize = new Vector2(1.1f, 0.42f);
        /// <summary>기본 자리: 정면 벽 안쪽, 창문 위. 읽는 면(-Z)이 가게 안(계산대 쪽)을 봄. 씬에 자리표("Sign/ShopClock")가 있으면 그 자리</summary>
        public static Pose DefaultPose => new Pose(new Vector3(0f, 2.6f, ShopLayout.FrontZ - ShopLayout.WallHalf - 0.03f), Quaternion.identity);
        [Tooltip("TMP 3D 글자 크기. 계산대에서 약 4m 거리라 크게")]
        [SerializeField] float fontSize = 0.9f;

        TextMeshPro text;
        TextMeshPro memoText;
        TextPlate memoPlate;
        SpriteRenderer barFill;
        GameObject root;
        string shown;
        string shownMemo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (GameContext.Instance == null || FindAnyObjectByType<ShopTimerBoard>() != null) return;
            new GameObject("ShopTimerBoard").AddComponent<ShopTimerBoard>();
        }

        protected override void Start()
        {
            base.Start();
            Pose pose = PlacementAnchor.Find(AnchorKey, DefaultPose);
            root = new GameObject("Board");
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(pose.position, pose.rotation);

            AddSprite("Frame", UiTheme.Wood, BoardSize, 0.05f, 0, 0f);
            AddSprite("Face", Lcd, BoardSize - new Vector2(FrameInset * 2f, FrameInset * 2f), 0.035f, 1, -0.001f);
            Color track = UiTheme.Ink;
            AddSprite("BarTrack", track, new Vector2(BarWidth, BarHeight), BarHeight * 0.5f, 2, -0.002f)
                .transform.localPosition = new Vector3(0f, BarY, -0.002f);
            barFill = AddSprite("BarFill", UiTheme.Accent, new Vector2(BarWidth, BarHeight), BarHeight * 0.5f, 3, -0.003f);

            text = MakeText("Time", new Vector3(0f, 0.03f, -0.004f), new Vector2(BoardSize.x - 0.2f, 0.26f), fontSize, 4);
            text.textWrappingMode = TextWrappingModes.NoWrap;

            // 청소 중 남은 곳을 알려 주는 노란 메모지 (오른쪽 위, 살짝 기울임)
            memoText = MakeText("CleanupMemo", new Vector3(0.38f, 0.07f, -0.01f), new Vector2(0.3f, 0.12f), 0.2f, 4);
            memoText.transform.localRotation = Quaternion.Euler(0f, 0f, -3f);
            memoPlate = TextPlate.Attach(memoText, NoteTape.Sticky, new Vector2(0.025f, 0.015f), 0.006f, default, 0.004f, false, 6);
            memoText.gameObject.SetActive(false);
            Refresh();
        }

        SpriteRenderer AddSprite(string name, Color color, Vector2 size, float cornerM, int order, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = UiSprites.Rounded(NoteTape.PpuFor(cornerM));
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        TextMeshPro MakeText(string name, Vector3 pos, Vector2 rect, float size, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            var t = go.AddComponent<TextMeshPro>();
            UiFonts.Apply(t);
            t.fontSize = size;
            t.color = UiTheme.Ink;
            t.alignment = TextAlignmentOptions.Center;
            ((RectTransform)go.transform).sizeDelta = rect;
            t.GetComponent<MeshRenderer>().sortingOrder = order;
            return t;
        }

        protected override void Refresh()
        {
            if (root != null) root.SetActive(CurrentPhase == GamePhaseId.Business);
        }

        void Update()
        {
            if (text == null || !(Flow != null && Flow.Current is BusinessPhase business)) return;
            int pending = Flow.Cleanup?.PendingCount ?? 0;
            float total = Context != null && Context.Session != null ? Context.Campaign.GetDay(Context.Session.Day).businessSeconds : 0f;
            float cleanup = Context != null ? Context.Campaign.closingCleanupSeconds : 30f;
            float hour = business.IsCleaningUp ? BusinessClockRules.HourAfterClose(cleanup - business.CleanupTimeLeft)
                : total > 0f ? BusinessClockRules.HourAt(1f - business.TimeLeft / total) : BusinessClockRules.OpenHour;
            string sub = business.IsCleaningUp ? $"청소 마감 {BusinessClockRules.Format(BusinessClockRules.HourAfterClose(cleanup))}"
                : business.IsClosing ? "마감! 남은 손님 계산하기"
                : $"마감 {BusinessClockRules.Format(BusinessClockRules.CloseHour)}";
            bool warn = !business.IsClosing && business.TimeLeft <= WarnSeconds || business.IsCleaningUp;
            text.color = warn && Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? WarnRed : UiTheme.Gold; // LCD 금색 숫자, 경고는 빨강 깜빡
            string message = "<mspace=0.6em><b>" + BusinessClockRules.Format(hour) + "</b></mspace>\n<size=32%><color=#CCC2EB>" + sub + "</color></size>";
            if (message != shown)
            {
                shown = message;
                text.text = message;
            }
            UpdateBar(business, total, warn);
            UpdateMemo(business.IsCleaningUp, pending);
        }

        // 마감까지 남은 비율만큼 왼쪽에 붙어 줄어드는 막대 (마감 · 청소 중에는 비어 있음)
        void UpdateBar(BusinessPhase business, float total, bool warn)
        {
            float ratio = business.IsClosing || business.IsCleaningUp || total <= 0f ? 0f : Mathf.Clamp01(business.TimeLeft / total);
            float width = BarWidth * ratio;
            float min = barFill.sprite.border.x * 2f / barFill.sprite.pixelsPerUnit; // 9-slice는 모서리 두 개보다 작으면 깨짐
            barFill.enabled = width >= min;
            if (!barFill.enabled) return;
            barFill.size = new Vector2(width, BarHeight);
            barFill.transform.localPosition = new Vector3(-BarWidth * 0.5f + width * 0.5f, BarY, -0.003f);
            barFill.color = warn ? WarnRed : UiTheme.Accent;
        }

        void UpdateMemo(bool cleaningUp, int pending)
        {
            memoText.gameObject.SetActive(cleaningUp);
            if (!cleaningUp) return;
            string note = $"치울 곳\n<b>{pending}군데!</b>";
            if (note == shownMemo) return;
            shownMemo = note;
            memoText.text = note;
            memoPlate.Fit();
        }
    }
}
