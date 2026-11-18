using System;
using System.Collections;
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
        Home = 8,         // 퇴근 후 집: 정리 · 판매 등록 · 커뮤니티 · 잠자기 (AfterHours와 DayEnd 사이)
    }

    /// <summary>페이즈를 바꾸는 동안 보여줄 장면 전환(페이드·이동). 구현은 Presentation의 DayTransition.</summary>
    public interface IPhaseTransition
    {
        /// <param name="switchPhase">화면이 완전히 가려진 뒤 호출해야 하는 실제 페이즈 전환</param>
        IEnumerator Play(Action switchPhase);
    }

    /// <summary>영업 중 매장의 손님들. 구현은 Customers의 CustomerDirector. 없으면 손님 없이 타이머만 돕니다.</summary>
    public interface ICustomerFloor
    {
        /// <summary>아직 매장에 남은 손님이 있거나 오늘 올 손님이 남았는가</summary>
        bool HasCustomers { get; }
        /// <summary>영업 시간이 끝남: 새 손님은 그만 오고, 남은 손님은 결제 대기만 처리</summary>
        void StopSpawning();
        /// <summary>디버그 · 강제 종료: 남은 손님을 모두 즉시 내보냄</summary>
        void DismissAll();
    }

    /// <summary>영업 중 청소 구역. 구현은 Cleanup의 CleanupDirector. 없으면 청소 없이 손님만 기다립니다.</summary>
    public interface ICleanupFloor
    {
        /// <summary>아직 치우지 않은 청소 구역 수</summary>
        int PendingCount { get; }
        /// <summary>영업 끝: 남은 청소 구역을 모두 걷어 내고, 치우지 못한 수를 돌려줍니다(감점용).</summary>
        int EndDay();
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
        bool isTransitioning;

        public GamePhaseId CurrentPhase => current?.Id ?? GamePhaseId.None;
        public IGamePhase Current => current;

        /// <summary>장면 전환 연출. 없으면 GoToFaded는 그냥 GoTo처럼 동작합니다.</summary>
        public IPhaseTransition Transition { get; set; }

        /// <summary>매장 손님. CustomerDirector가 Awake에서 등록합니다.</summary>
        public ICustomerFloor Customers { get; set; }

        /// <summary>청소 구역. CleanupDirector가 OnEnable에서 등록합니다.</summary>
        public ICleanupFloor Cleanup { get; set; }

        /// <summary>전환 연출 중에는 버튼 입력을 받지 않습니다.</summary>
        public bool IsTransitioning => isTransitioning;

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
            Register(new HomePhase(this, context));
            Register(new DayEndPhase(this, context));
            Register(new FinalPhase(this, context));
        }

        void Start() => GoTo(GamePhaseId.Title);

        void Update()
        {
            if (!IsPaused) current?.Tick(Time.deltaTime); // 일시정지 중에는 어느 페이즈의 시간도 흐르지 않음
        }

        // 헤드셋을 벗거나(Pause) Quest 시스템 메뉴가 열리면(포커스 상실) 영업 타이머를 멈춥니다.
        // 되돌리는 건 PauseMenu의 [이어서 하기] → Resume() (예전에는 되돌리는 코드가 없어 영업이 영영 멈췄음).
        void OnApplicationPause(bool pause)
        {
            if (pause) Pause();
        }

#if !UNITY_EDITOR
        void OnApplicationFocus(bool focus)
        {
            if (!focus) Pause();
        }
#endif

        /// <summary>일시정지. 어느 페이즈에서나 됩니다 (3차 기기 피드백: 예전엔 영업 중에만 됐음). 멈추는 것은 영업 타이머 · 손님 · 거리 행인.</summary>
        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume() => IsPaused = false;

        /// <summary>처음부터 다시: 새 세션을 만들고 타이틀로 돌아갑니다. (타이틀에서 시작을 누르면 인트로)</summary>
        public void RestartGame()
        {
            IsPaused = false;
            context.StartNewSession();
            GoTo(GamePhaseId.Title);
        }

        void Register(IGamePhase phase) => phases[phase.Id] = phase;

        public T GetPhase<T>(GamePhaseId id) where T : class, IGamePhase => phases[id] as T;

        public void GoTo(GamePhaseId next)
        {
            GamePhaseId prev = CurrentPhase;
            // 일시정지는 페이즈가 바뀌어도 그대로 둠: 메뉴가 떠 있는 채로 버튼을 눌러 페이즈가 바뀌어도 메뉴가 갑자기 사라지지 않게.
            // 새 판(RestartGame)에서만 풉니다.
            current?.Exit();
            current = phases[next];
            context.Events.RaisePhaseChanged(prev, next);
            current.Enter();
        }

        /// <summary>화면 전환 연출(페이드 아웃 → 전환·이동 → 페이드 인)과 함께 페이즈를 바꿉니다.</summary>
        public void GoToFaded(GamePhaseId next)
        {
            if (Transition == null) { GoTo(next); return; }
            if (isTransitioning) return;
            StartCoroutine(FadedRoutine(next));
        }

        IEnumerator FadedRoutine(GamePhaseId next)
        {
            isTransitioning = true;
            yield return Transition.Play(() => GoTo(next));
            isTransitioning = false;
        }

        /// <summary>시작·확인·퇴근 버튼 등에서 호출.</summary>
        public void RequestAdvance()
        {
            if (isTransitioning) return;
            current?.OnAdvanceRequested();
        }

        /// <summary>침대 버튼(Poke). 집에서만 동작합니다.</summary>
        public void Sleep()
        {
            if (CurrentPhase == GamePhaseId.Home) RequestAdvance();
            else context.Events.RaiseNotice("잠은 퇴근하고 집에서 잘 수 있어요.");
        }

        /// <summary>퇴근 버튼(Poke). 퇴근 전 페이즈에서만 동작합니다.</summary>
        public void ClockOut()
        {
            if (CurrentPhase == GamePhaseId.AfterHours) RequestAdvance();
            else context.Events.RaiseNotice("퇴근은 영업과 정산이 끝난 뒤에 할 수 있어요.");
        }

        [ContextMenu("Debug/Advance")]
        void DebugAdvance() => RequestAdvance();
    }
}
