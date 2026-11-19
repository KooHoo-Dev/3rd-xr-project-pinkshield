using CardShop.Core;
using CardShop.Core.Phases;
using CardShop.Presentation;
using CardShop.Tutorial;
using TMPro;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 가게 전광판 (빌드피드백_11-19_FIX설계 B5): 계산대에서 정면(창문 위 벽)에 영업 남은 시간 · 마감 청소 남은 시간을 보여 줍니다.
    /// 바늘 시계(ShopClock)는 플레이어 뒤쪽이라 안 보이고, 남은 시간 글자는 디버그 판에만 있어 출시 빌드에서는 마감 청소 안내가 없었음.
    /// 영업 중에만 켜지고, 씬을 다시 빌드하지 않아도 되게 씬이 열리면 스스로 만들어집니다.
    /// </summary>
    public sealed class ShopTimerBoard : PhaseListener
    {
        const float WarnSeconds = 30f; // 이 아래면 분홍으로 깜빡임
        static readonly Color Ink = UiTheme.Text;
        static readonly Color Warn = UiTheme.Accent;

        [Tooltip("판 자리 (월드): 정면 벽 안쪽, 창문 위")]
        [SerializeField] Vector3 position = new Vector3(0f, 2.6f, ShopLayout.FrontZ - ShopLayout.WallHalf - 0.03f);
        [SerializeField] Vector2 size = new Vector2(1.5f, 0.34f);
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
            // 읽는 면(-Z)이 가게 안(계산대 쪽, -Z 방향)을 보게
            WorldSign sign = WorldSign.Create(transform, "Board", revealer != null ? revealer.Font : null,
                new Pose(position, Quaternion.identity), size, UiTheme.Panel, Ink, fontSize, "");
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
            float left = business.IsCleaningUp ? business.CleanupTimeLeft : business.TimeLeft;
            string message = business.IsCleaningUp ? $"마감 청소 {Clock(left)}  ·  남은 곳 {pending}"
                : business.IsClosing ? "마감! 남은 손님 계산하기"
                : $"마감까지 {Clock(left)}";
            bool warn = !business.IsClosing && left <= WarnSeconds || business.IsCleaningUp;
            text.color = warn && Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? Warn : Ink;
            if (message == shown) return;
            shown = message;
            text.text = message;
        }

        static string Clock(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
