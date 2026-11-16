using System.Collections.Generic;
using CardShop.Core.Phases;
using UnityEngine;

namespace CardShop.Core
{
    public enum GamePhaseId
    {
        None = 0,
        Title = 1,
        Intro = 2,        // 1일차: 사장님 첫 팩 · 업무 소개
        Business = 3,     // 영업
        Settlement = 4,   // 일일 정산
        AfterHours = 5,   // 무료 팩 · 구매 · 개봉 · 진열
        DayEnd = 6,       // 미판매 반환 · 다음 날 준비
        Final = 7,        // 최종 정산
    }

    public interface IGamePhase
    {
        GamePhaseId Id { get; }
        void Enter();
        void Tick(float deltaTime);
        void Exit();
        /// <summary>UI 버튼(시작, 확인, 퇴근 등)이 "다음으로"를 요청할 때.</summary>
        void OnAdvanceRequested();
    }

    /// <summary>
    /// 페이즈 상태기계 (설계서 6장). 페이즈 하나 = 클래스 하나라 흐름 수정이 국소적입니다.
    /// 일시정지는 Time.timeScale을 쓰지 않습니다(XRI Grab·Socket이 멈춤). IsPaused 플래그로 타이머·손님만 멈춥니다.
    /// </summary>
    [RequireComponent(typeof(GameContext))]
    public sealed class GameFlow : MonoBehaviour
    {
        GameContext context;
        readonly Dictionary<GamePhaseId, IGamePhase> phases = new Dictionary<GamePhaseId, IGamePhase>();
        IGamePhase current;
        bool isPaused;

        public GamePhaseId CurrentPhase => current?.Id ?? GamePhaseId.None;
        public IGamePhase Current => current;

        public bool IsPaused
        {
            get => isPaused;
            set
            {
                if (isPaused == value) return;
                isPaused = value;
                context.Events.RaisePausedChanged(value);
            }
        }

        void Awake()
        {
            context = GetComponent<GameContext>();
            Register(new TitlePhase(this, context));
            Register(new IntroPhase(this, context));
            Register(new BusinessPhase(this, context));
            Register(new SettlementPhase(this, context));
            Register(new AfterHoursPhase(this, context));
            Register(new DayEndPhase(this, context));
            Register(new FinalPhase(this, context));
        }

        void Start() => GoTo(GamePhaseId.Title);

        void Update() => current?.Tick(Time.deltaTime);

        // 헤드셋을 벗으면 영업 타이머를 멈춥니다.
        void OnApplicationPause(bool pause)
        {
            if (pause) IsPaused = true;
        }

        void Register(IGamePhase phase) => phases[phase.Id] = phase;

        public T GetPhase<T>(GamePhaseId id) where T : class, IGamePhase => phases[id] as T;

        public void GoTo(GamePhaseId next)
        {
            GamePhaseId prev = CurrentPhase;
            current?.Exit();
            current = phases[next];
            context.Events.RaisePhaseChanged(prev, next);
            current.Enter();
        }

        /// <summary>시작·확인·퇴근 버튼 등에서 호출.</summary>
        public void RequestAdvance() => current?.OnAdvanceRequested();

        [ContextMenu("Debug/Advance")]
        void DebugAdvance() => RequestAdvance();
    }
}
