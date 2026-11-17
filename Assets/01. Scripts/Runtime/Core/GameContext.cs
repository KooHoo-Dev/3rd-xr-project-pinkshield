using System;
using CardShop.Cards;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Core
{
    /// <summary>
    /// 씬의 유일한 진입점(설계서 3장). 설정 SO를 받고, 한 판의 상태와 규칙 서비스를 만들어 보관합니다.
    /// 다른 컴포넌트는 GameContext.Instance 또는 인스펙터 참조로 접근합니다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameContext : MonoBehaviour
    {
        public static GameContext Instance { get; private set; }

        [Header("Configs (Assets/02. Data)")]
        [SerializeField] CardDatabase cardDatabase;
        [SerializeField] RarityTable rarityTable;
        [SerializeField] PackOddsConfig packOdds;
        [SerializeField] EconomyConfig economy;
        [SerializeField] CampaignConfig campaign;
        [SerializeField] ScoreConfig score;

        [Header("Random")]
        [Tooltip("0이면 매번 다른 시드")]
        [SerializeField] int fixedSeed;

        public CardDatabase Cards => cardDatabase;
        public RarityTable Rarity => rarityTable;
        public PackOddsConfig PackOdds => packOdds;
        public EconomyConfig Economy => economy;
        public CampaignConfig Campaign => campaign;
        public ScoreConfig Score => score;

        public GameEvents Events { get; } = new GameEvents();
        public GameSession Session { get; private set; }
        public IRandomSource Random { get; set; }
        public EconomyRules EconomyRules { get; private set; }
        public PackRoller PackRoller { get; private set; }
        public PackOpeningService PackOpening { get; private set; }
        public SalesRules Sales { get; private set; }
        public ScoreRules ScoreRules { get; private set; }
        /// <summary>새 게임마다 다시 뽑는 일차별 인기 속성 일정.</summary>
        public PopularElementSchedule PopularElements { get; private set; }

        /// <summary>오늘의 인기 속성.</summary>
        public CardElement PopularToday => PopularElements.For(Session.Day);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[GameContext] 씬에 GameContext가 2개 있습니다.", this);
                enabled = false;
                return;
            }
            Instance = this;

            Random = fixedSeed != 0 ? new SystemRandomSource(fixedSeed) : new SystemRandomSource();
            EconomyRules = new EconomyRules(economy, rarityTable);
            PackRoller = new PackRoller(packOdds, cardDatabase, Random);
            PackOpening = new PackOpeningService(() => Session, PackRoller, economy);
            Sales = new SalesRules(economy, rarityTable, EconomyRules, cardDatabase, Random);
            ScoreRules = new ScoreRules(score, rarityTable, cardDatabase);
            StartNewSession();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>새 게임. 이전 세션의 구독은 Events.SessionStarted에서 다시 연결하세요.</summary>
        public void StartNewSession()
        {
            Session = new GameSession(economy.startingMoney);
            PopularElements = new PopularElementSchedule(campaign.DayCount, economy.popularCycleDays, Random);
            Events.RaiseSessionStarted(Session);
        }
    }

    /// <summary>
    /// 시스템 간 이벤트 모음. static이 아니므로 Enter Play Mode(도메인 리로드 끔)에서도 구독이 남지 않습니다.
    /// 기능이 늘어날 때 여기에 추가합니다.
    /// </summary>
    public sealed class GameEvents
    {
        public event Action<GameSession> SessionStarted;
        public event Action<GamePhaseId, GamePhaseId> PhaseChanged; // (이전, 다음)
        public event Action<int> DayStarted;
        public event Action<bool> PausedChanged;
        /// <summary>팩 1개 이상을 연 직후 (단일 팩은 1개, 상자는 10개).</summary>
        public event Action<PackResult[]> PacksOpened;
        /// <summary>플레이어에게 보여줄 짧은 안내 (재화 부족 등). 지금은 디버그 패널, 나중에 사장님 말풍선.</summary>
        public event Action<string> Notice;
        /// <summary>손님 결제 완료. 진열 카드를 샀으면 그 카드, 가게 재고를 샀으면 null.</summary>
        public event Action<CardData> CustomerServed;
        /// <summary>손님이 매장을 나감 (매장 안 마지막 위치). 청소 구역 생성용.</summary>
        public event Action<Vector3> CustomerLeft;

        public void RaisePacksOpened(PackResult[] packs) => PacksOpened?.Invoke(packs);
        public void RaiseCustomerServed(CardData boughtCard) => CustomerServed?.Invoke(boughtCard);
        public void RaiseCustomerLeft(Vector3 lastPosition) => CustomerLeft?.Invoke(lastPosition);
        public void RaiseNotice(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log($"[Notice] {message}"); // 콘솔에서도 흐름을 따라갈 수 있게
#endif
            Notice?.Invoke(message);
        }

        internal void RaiseSessionStarted(GameSession s) => SessionStarted?.Invoke(s);
        internal void RaisePhaseChanged(GamePhaseId from, GamePhaseId to) => PhaseChanged?.Invoke(from, to);
        internal void RaiseDayStarted(int day) => DayStarted?.Invoke(day);
        internal void RaisePausedChanged(bool paused) => PausedChanged?.Invoke(paused);
    }
}
