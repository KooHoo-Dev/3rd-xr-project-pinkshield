using CardShop.Core;
using CardShop.Presentation;
using CardShop.UI;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 언제든 다시 볼 수 있는 안내 판 두 개를 세웁니다 (튜토리얼_설계 5-1 · 5-4, 환경 단서). 튜토리얼을 꺼도 그대로 둡니다.
    /// ① 조작 안내 판: 가게 앞 인도의 타이틀 판 옆, 타이틀 · 인트로에서만 보임(11-19 A5: 영업 중에도 계속 보였음). ② 알바 수칙 포스터: 계산대 뒤 벽, 늘 보임.
    /// 씬을 다시 빌드하지 않도록 씬이 열리면 스스로 만들어집니다.
    /// 자리: 씬의 자리표(<see cref="PlacementAnchor"/> "Sign/Controls" · "Sign/Rules")가 있으면 그 자리, 없으면 아래 기본 자리.
    /// 자리표는 Tools > CardShop > Map Edit 창에서 만들고 옮깁니다 (맵_수정_도구_설계).
    /// </summary>
    public sealed class TutorialSigns : MonoBehaviour
    {
        public const string ControlsKey = "Sign/Controls";
        public const string RulesKey = "Sign/Rules";
        public static readonly Vector2 ControlsSize = new Vector2(0.8f, 0.42f);
        public static readonly Vector2 RulesSize = new Vector2(0.6f, 0.42f);

        /// <summary>조작 안내 판 기본 자리: 타이틀 판 왼쪽 1.05m. 타이틀 판은 −Z 쪽에서 읽힘(플레이어가 −Z를 봄) → 같은 방향</summary>
        public static Pose ControlsDefault => new Pose(ShopLayout.TitleBoardPos + new Vector3(-1.05f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
        /// <summary>알바 수칙 포스터 기본 자리: 계산대 뒤 벽 앞. 플레이어(원점)가 뒤돌아 −Z를 보며 읽음 → 앞면(-Z)이 +Z 쪽을 향하게</summary>
        public static Pose RulesDefault => new Pose(new Vector3(-0.9f, 1.55f, ShopLayout.BackZ + ShopLayout.WallHalf + 0.02f), Quaternion.Euler(0f, 180f, 0f));

        // 조작 안내 = 타이틀 칠판과 같은 칠판 판 (11-20): 버튼 이름은 금색 · 메뉴는 분홍 글자로 강조.
        // (예전 <mark> 색 칸은 TMP가 글자 위에 그려 글자를 덮어 깨져 보였음)
        static readonly string ControlsText =
            "<color=#FFD159>조작 안내</color>\n" +
            Key("그립") + " — 잡기 (가운뎃손가락)\n" +
            Key("트리거") + " — 누르기 · 카드 넘기기 (검지)\n" +
            Key("왼쪽 스틱") + " — 걷기\n" +
            Key("오른쪽 스틱") + " — 돌기\n" +
            Key("메뉴", "#FF8CBF") + " — 일시정지 · 안내 다시 보기 (왼손)";

        static string Key(string name, string color = "#FFD159") => $"<color={color}>{name}</color>";

        static string RulesText(GameContext ctx)
        {
            int bonus = ctx != null ? ctx.Economy.cleanupBonus : 5;
            int penalty = ctx != null ? ctx.Economy.cleanupPenalty : 3;
            int days = ctx != null ? ctx.Campaign.DayCount : 7;
            return "<b><u>알바 수칙</u></b>\n" +
                   "1. 손님이 오면 계산부터\n" +
                   "   (찍기 → 봉투 → 결제)\n" +
                   $"2. 청소 한 곳 +{bonus}, 못 하면 -{penalty}\n" +
                   "3. 빨리 계산하면 보너스\n" +
                   $"4. {days}일 평가 B 이상이면 정직원!\n" +
                   "   <size=80%>(점수 = 돈 + 카드 매입가 + 도감 + 방 꾸미기)</size>\n" +
                   "5. 좋은 카드는 가게에서 팔기\n" +
                   "   (바로 팔면 매입가)\n" +
                   "<align=right>— 사장</align>";
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
            // 조작 안내 = 타이틀 칠판 옆에 서는 칠판 판 (나무 틀 + 칠판 + 미색 글자, 11-20). 수칙 포스터는 종이 + 테이프
            controlsBoard = WorldSign.Create(transform, "ControlsBoard", font, PlacementAnchor.Find(ControlsKey, ControlsDefault),
                ControlsSize, UiTheme.Chalkboard, UiTheme.Text, 0.26f, ControlsText, false, 0f, UiTheme.Wood, 0.04f);

            WorldSign.Create(transform, "RulesPoster", font, PlacementAnchor.Find(RulesKey, RulesDefault),
                RulesSize, UiTheme.Paper, UiTheme.Ink, 0.24f, RulesText(GameContext.Instance), true, -2f);

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

        // 영업 시작을 누르면 화면 전환(페이드)이 끝나 페이즈가 바뀌기 전에 바로 숨김 (11-20 D3: 0.5초 늦게 사라졌음)
        void Update()
        {
            if (controlsBoard == null || !controlsBoard.gameObject.activeSelf || subscribed == null) return;
            GameFlow flow = subscribed.GetComponent<GameFlow>();
            if (flow != null && flow.IsTransitioning) controlsBoard.gameObject.SetActive(false); // 다음 페이즈가 타이틀 · 인트로면 바뀔 때 다시 켜짐
        }

        void ShowControls(GamePhaseId phase)
        {
            if (controlsBoard != null) controlsBoard.gameObject.SetActive(phase == GamePhaseId.Title || phase == GamePhaseId.Intro);
        }
    }
}
