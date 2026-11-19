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
    /// 가게 디지털 시계 (빌드피드백_11-19_FIX설계 B5 → 11-20 F6). 계산대 정면(창문 위 벽)의 LED 시계가 게임 시각을 보여 줍니다.
    /// 남은 초를 글자로 보여 주던 판 대신 세상 속 물건으로: 영업 2분 남짓 = 09:00 → 16:00, 마감 청소는 16:00부터 1초 = 1분.
    /// 아래 작은 줄에 마감 · 청소 마감 시각과 남은 곳을 적고, 30초 아래 · 청소 중에는 숫자가 깜빡입니다.
    /// 영업 중에만 켜지고, 씬을 다시 빌드하지 않아도 되게 씬이 열리면 스스로 만들어집니다.
    /// </summary>
    public sealed class ShopTimerBoard : PhaseListener
    {
        const float WarnSeconds = 30f; // 이 아래면 분홍으로 깜빡임
        static readonly Color Ink = new Color(1f, 0.55f, 0.25f);  // 주황 LED
        static readonly Color Warn = new Color(1f, 0.25f, 0.3f);  // 빨강 LED
        static readonly Color Bezel = new Color(0.05f, 0.05f, 0.07f, 1f);

        public const string AnchorKey = "Sign/ShopClock";
        public static readonly Vector2 BoardSize = new Vector2(1.1f, 0.42f);
        /// <summary>기본 자리: 정면 벽 안쪽, 창문 위. 읽는 면(-Z)이 가게 안(계산대 쪽)을 봄. 씬에 자리표("Sign/ShopClock")가 있으면 그 자리</summary>
        public static Pose DefaultPose => new Pose(new Vector3(0f, 2.6f, ShopLayout.FrontZ - ShopLayout.WallHalf - 0.03f), Quaternion.identity);
        [Tooltip("TMP 3D 글자 크기. 계산대에서 약 4m 거리라 크게")]
        [SerializeField] float fontSize = 1.0f;

        TextMeshPro text;
        string shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (GameContext.Instance == null || FindAnyObjectByType<ShopTimerBoard>() != null) return;
            new GameObject("ShopTimerBoard").AddComponent<ShopTimerBoard>();
        }

        protected override void Start()
        {
            base.Start();
            var revealer = FindAnyObjectByType<CardRevealer>(); // 한글 글꼴은 카드 프리팹 것을 빌림
            WorldSign sign = WorldSign.Create(transform, "Board", revealer != null ? revealer.Font : null,
                PlacementAnchor.Find(AnchorKey, DefaultPose), BoardSize, Bezel, Ink, fontSize, "");
            text = sign.Text;
            text.alignment = TextAlignmentOptions.Center;
            Refresh();
        }

        protected override void Refresh()
        {
            if (text != null) text.transform.parent.gameObject.SetActive(CurrentPhase == GamePhaseId.Business);
        }

        void Update()
        {
            if (text == null || !(Flow != null && Flow.Current is BusinessPhase business)) return;
            int pending = Flow.Cleanup?.PendingCount ?? 0;
            float total = Context != null && Context.Session != null ? Context.Campaign.GetDay(Context.Session.Day).businessSeconds : 0f;
            float cleanup = Context != null ? Context.Campaign.closingCleanupSeconds : 30f;
            float hour = business.IsCleaningUp ? BusinessClockRules.HourAfterClose(cleanup - business.CleanupTimeLeft)
                : total > 0f ? BusinessClockRules.HourAt(1f - business.TimeLeft / total) : BusinessClockRules.OpenHour;
            string sub = business.IsCleaningUp ? $"청소 마감 {BusinessClockRules.Format(BusinessClockRules.HourAfterClose(cleanup))} · 남은 곳 {pending}"
                : business.IsClosing ? "마감! 남은 손님 계산하기"
                : $"마감 {BusinessClockRules.Format(BusinessClockRules.CloseHour)}";
            bool warn = !business.IsClosing && business.TimeLeft <= WarnSeconds || business.IsCleaningUp;
            text.color = warn && Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? Warn : Ink;
            string message = "<mspace=0.6em><b>" + BusinessClockRules.Format(hour) + "</b></mspace>\n<size=32%>" + sub + "</size>";
            if (message == shown) return;
            shown = message;
            text.text = message;
        }
    }
}
