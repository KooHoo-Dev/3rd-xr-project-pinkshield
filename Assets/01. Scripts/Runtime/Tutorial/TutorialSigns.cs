using CardShop.Core;
using CardShop.Presentation;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 언제든 다시 볼 수 있는 안내 판 두 개를 세웁니다 (튜토리얼_설계 5-1 · 5-4, 환경 단서). 튜토리얼을 꺼도 그대로 둡니다.
    /// ① 조작 안내 판: 가게 앞 인도의 타이틀 판 옆, 타이틀 · 인트로에서만 보임(11-19 A5: 영업 중에도 계속 보였음). ② 알바 수칙 포스터: 계산대 뒤 벽, 늘 보임.
    /// 씬을 다시 빌드하지 않도록 씬이 열리면 스스로 만들어집니다. 자리는 인스펙터 값으로 옮길 수 있습니다.
    /// </summary>
    public sealed class TutorialSigns : MonoBehaviour
    {
        [Tooltip("조작 안내 판 자리 = 타이틀 판 자리 + 이 값 (m)")]
        [SerializeField] Vector3 controlsOffset = new Vector3(-1.05f, 0f, 0f);
        [Tooltip("알바 수칙 포스터 자리 (월드, 계산대 뒤 벽 앞)")]
        [SerializeField] Vector3 rulesPosition = new Vector3(-0.9f, 1.55f, ShopLayout.BackZ + ShopLayout.WallHalf + 0.02f);

        const string ControlsText =
            "<b>조작 안내</b>\n" +
            "그립 (가운뎃손가락) — 잡기\n" +
            "트리거 (검지) — 누르기 · 카드 넘기기\n" +
            "왼쪽 스틱 — 걷기\n" +
            "오른쪽 스틱 — 돌기\n" +
            "왼손 메뉴 버튼 — 일시정지 · 안내 다시 보기";

        static string RulesText(GameContext ctx)
        {
            int bonus = ctx != null ? ctx.Economy.cleanupBonus : 5;
            int penalty = ctx != null ? ctx.Economy.cleanupPenalty : 3;
            int days = ctx != null ? ctx.Campaign.DayCount : 7;
            return "<b>알바 수칙</b>\n" +
                   "1. 손님이 오면 계산부터\n" +
                   "   (찍기 → 봉투 → 결제)\n" +
                   $"2. 청소 한 곳 +{bonus}, 못 하면 -{penalty}\n" +
                   "3. 빨리 계산하면 보너스\n" +
                   $"4. {days}일 평가 B 이상이면 정직원!";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (GameContext.Instance == null || FindAnyObjectByType<TutorialSigns>() != null) return;
            new GameObject("TutorialSigns").AddComponent<TutorialSigns>();
        }

        WorldSign controlsBoard;
        GameContext subscribed;

        void Start()
        {
            var revealer = FindAnyObjectByType<CardRevealer>();
            var font = revealer != null ? revealer.Font : null;
            var paper = new Color(1f, 0.97f, 0.88f);
            var ink = new Color(0.18f, 0.15f, 0.22f);

            // 타이틀 판은 −Z 쪽에서 읽힘(플레이어가 −Z를 봄) → 같은 방향
            Vector3 controls = ShopLayout.TitleBoardPos + controlsOffset;
            controlsBoard = WorldSign.Create(transform, "ControlsBoard", font, new Pose(controls, Quaternion.Euler(0f, 180f, 0f)),
                new Vector2(0.8f, 0.42f), new Color(0.12f, 0.1f, 0.16f, 0.92f), Color.white, 0.26f, ControlsText);

            // 계산대 뒤 벽: 플레이어(원점)가 뒤돌아 −Z를 보며 읽음 → 앞면(-Z)이 +Z 쪽을 향하게
            WorldSign.Create(transform, "RulesPoster", font, new Pose(rulesPosition, Quaternion.Euler(0f, 180f, 0f)),
                new Vector2(0.6f, 0.42f), paper, ink, 0.24f, RulesText(GameContext.Instance));

            subscribed = GameContext.Instance;
            if (subscribed == null) return;
            subscribed.Events.PhaseChanged += OnPhaseChanged;
            GameFlow flow = subscribed.GetComponent<GameFlow>();
            ShowControls(flow == null || flow.CurrentPhase == GamePhaseId.None ? GamePhaseId.Title : flow.CurrentPhase);
        }

        void OnDestroy()
        {
            if (subscribed != null) subscribed.Events.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(GamePhaseId from, GamePhaseId to) => ShowControls(to);

        void ShowControls(GamePhaseId phase)
        {
            if (controlsBoard != null) controlsBoard.gameObject.SetActive(phase == GamePhaseId.Title || phase == GamePhaseId.Intro);
        }
    }
}
