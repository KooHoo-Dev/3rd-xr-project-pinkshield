using CardShop.Cards;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Core.Phases
{
    // 1일차 뼈대: 각 페이즈는 "끝나는 조건"만 갖고 있습니다. 기능이 붙으면 페이즈별 파일로 분리하세요.

    public abstract class GamePhase : IGamePhase
    {
        protected readonly GameFlow Flow;
        protected readonly GameContext Context;

        protected GamePhase(GameFlow flow, GameContext context)
        {
            Flow = flow;
            Context = context;
        }

        public abstract GamePhaseId Id { get; }
        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Exit() { }
        public virtual void OnAdvanceRequested() { }

        protected void Log(string msg) => Debug.Log($"[{Id}] Day {Context.Session.Day}: {msg}");
    }

    /// <summary>타이틀. 시작 버튼 → 새 세션, 1일차 인트로.</summary>
    public sealed class TitlePhase : GamePhase
    {
        public TitlePhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.Title;

        public override void OnAdvanceRequested()
        {
            Context.StartNewSession();
            Flow.GoTo(GamePhaseId.Intro);
        }
    }

    /// <summary>1일차 사장님 첫 팩 · 업무 소개. TODO(5일차): 튜토리얼 완료 시 진행.</summary>
    public sealed class IntroPhase : GamePhase
    {
        public IntroPhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.Intro;

        public override void Enter()
        {
            Context.Events.RaiseDayStarted(Context.Session.Day);
            Log("사장님 첫 팩 (튜토리얼 미구현: Advance로 넘어감)");
        }

        public override void OnAdvanceRequested() => Flow.GoTo(GamePhaseId.Business);
    }

    /// <summary>
    /// 영업. 타이머 종료 → 새 손님 중단, 남은 손님은 결제 대기만 처리(설계서 Q1) → 마지막 손님 퇴장 → 정산.
    /// TODO(청소): 마지막 손님 퇴장 후 마감 정리 시간(CampaignConfig.closingCleanupSeconds)은 청소가 생기면 추가.
    /// </summary>
    public sealed class BusinessPhase : GamePhase
    {
        public BusinessPhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.Business;

        public float TimeLeft { get; private set; }
        /// <summary>타이머는 끝났고 남은 손님을 기다리는 중</summary>
        public bool IsClosing { get; private set; }

        public override void Enter()
        {
            TimeLeft = Context.Campaign.GetDay(Context.Session.Day).businessSeconds;
            IsClosing = false;
            Log($"영업 시작 ({TimeLeft:0}초)");
        }

        public override void Tick(float deltaTime)
        {
            if (Flow.IsPaused) return;
            if (!IsClosing)
            {
                TimeLeft -= deltaTime;
                if (TimeLeft > 0f) return;
                TimeLeft = 0f;
                IsClosing = true;
                Flow.Customers?.StopSpawning();
                Log("영업 시간 종료 (남은 손님 결제 대기)");
            }
            if (Flow.Customers == null || !Flow.Customers.HasCustomers) Flow.GoTo(GamePhaseId.Settlement);
        }

        /// <summary>디버그: 영업 즉시 종료 (남은 손님도 내보냄)</summary>
        public override void OnAdvanceRequested()
        {
            TimeLeft = 0f;
            Flow.Customers?.DismissAll();
        }
    }

    /// <summary>일일 정산. 기본급 기록 → 합계 지급 → 확인 버튼.</summary>
    public sealed class SettlementPhase : GamePhase
    {
        public SettlementPhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.Settlement;

        public int LastTotal { get; private set; }

        public override void Enter()
        {
            var ledger = Context.Session.Ledger;
            ledger.basePay = Context.Economy.dailyBasePay;
            LastTotal = Context.EconomyRules.SettlementTotal(ledger);
            Context.Session.Wallet.Add(LastTotal);
            Log($"정산 +{LastTotal} → 보유 {Context.Session.Wallet.Money}");
        }

        public override void OnAdvanceRequested() => Flow.GoTo(GamePhaseId.AfterHours);
    }

    /// <summary>퇴근 전: 무료 팩 · 구매 · 개봉 · 진열. 퇴근 버튼(설계서 Q4).</summary>
    public sealed class AfterHoursPhase : GamePhase
    {
        public AfterHoursPhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.AfterHours;

        public override void Enter()
        {
            Context.Session.FreePacksLeft = Context.Economy.freePacksPerDay;
            Log("퇴근 전 시간 (퇴근 버튼 = Advance)");
        }

        // 퇴근: 마지막 날은 집을 건너뛰고 바로 최종 정산(DayEnd → Final), 아니면 집으로.
        public override void OnAdvanceRequested()
        {
            bool lastDay = Context.Session.Day >= Context.Campaign.DayCount;
            Flow.GoToFaded(lastDay ? GamePhaseId.DayEnd : GamePhaseId.Home);
        }
    }

    /// <summary>
    /// 퇴근 후 집 (시연 피드백). 안 팔린 진열 카드는 집으로 가져옵니다(반환 시점을 DayEnd에서 옮김:
    /// 집에서 내일 팔 카드를 다시 등록해도 DayEnd가 지우지 않도록). 책상(바인더) · 컴퓨터(커뮤니티)는 이후 단계에서 연결.
    /// 침대 버튼 = Advance → 다음 날 아침.
    /// </summary>
    public sealed class HomePhase : GamePhase
    {
        public HomePhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.Home;

        public override void Enter()
        {
            int returned = Context.Sales.ReturnUnsold(Context.Session);
            Log(returned > 0 ? $"안 팔린 진열 카드 {returned}장을 집으로 가져옴" : "집에 도착");
        }

        public override void OnAdvanceRequested() => Flow.GoToFaded(GamePhaseId.DayEnd);
    }

    /// <summary>하루 마무리. 미판매 진열 카드는 HomePhase가 이미 돌려놨고, 여기서는 마지막 날에만 돌려놓습니다. 선반 재배치는 DayStarted를 받은 선반이 합니다. 마지막 날이면 최종 정산.</summary>
    public sealed class DayEndPhase : GamePhase
    {
        public DayEndPhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.DayEnd;

        public override void Enter()
        {
            // 평소의 반환은 HomePhase가 합니다. 여기서 또 하면 집에서 등록한 카드가 사라지므로 마지막 날만(최종 점수용).
            int returned = Context.Sales.ReturnUnsoldOnFinalDay(Context.Session, Context.Campaign.DayCount);
            if (returned > 0) Log($"안 팔린 진열 카드 {returned}장 반환 (마지막 날)");

            if (Context.Session.Day >= Context.Campaign.DayCount)
            {
                Flow.GoTo(GamePhaseId.Final);
                return;
            }
            Context.Session.AdvanceDay();
            Context.Events.RaiseDayStarted(Context.Session.Day);
            Flow.GoTo(GamePhaseId.Business);
        }
    }

    /// <summary>최종 정산 · 랭크 · 엔딩 (기획서 5-8). 화면 UI는 5일차.</summary>
    public sealed class FinalPhase : GamePhase
    {
        public FinalPhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.Final;

        public FinalResult Result { get; private set; }

        public override void Enter()
        {
            Result = Context.ScoreRules.Calculate(Context.Session);
            ScoreConfig.Rank rank = Result.Rank;
            Log($"최종 {Result.Score}점 (재화 {Result.Money} + 카드 {Result.CardValue} + 도감 {Result.CollectionPoints}), " +
                $"랭크 {rank.name}, {(rank.isSuccess ? Context.Score.successEnding : Context.Score.failEnding)}");
        }

        public override void OnAdvanceRequested() => Flow.GoTo(GamePhaseId.Title);
    }
}
