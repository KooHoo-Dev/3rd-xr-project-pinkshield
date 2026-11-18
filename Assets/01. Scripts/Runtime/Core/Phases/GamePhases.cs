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

    /// <summary>
    /// 타이틀. 플레이어는 가게 앞 인도에 서 있고(DayTransition), 타이틀 판의 [영업 시작] → 새 세션 → 페이드 → 1일차 인트로.
    /// </summary>
    public sealed class TitlePhase : GamePhase
    {
        public TitlePhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.Title;

        public override void OnAdvanceRequested()
        {
            Context.StartNewSession();
            Flow.GoToFaded(GamePhaseId.Intro);
        }
    }

    /// <summary>
    /// 1일차 사장님 첫 팩 · 업무 소개. 튜토리얼(TODO)이 생기기 전까지는 첫 Tick에 바로 영업으로 넘어갑니다
    /// (출시 빌드에는 디버그 "다음" 버튼이 없어 여기서 멈췄음 — 타이틀연출_손님_설계 0-3).
    /// </summary>
    public sealed class IntroPhase : GamePhase
    {
        public IntroPhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.Intro;

        public override void Enter()
        {
            Context.Events.RaiseDayStarted(Context.Session.Day);
            Log("사장님 첫 팩 (튜토리얼 미구현: 바로 영업으로)");
        }

        // Enter 안에서 바로 GoTo하면 PhaseChanged 구독자가 Intro → Business를 한 호출 안에서 겹쳐 받으므로 한 프레임 뒤에 넘김
        public override void Tick(float deltaTime) => Flow.GoTo(GamePhaseId.Business);

        public override void OnAdvanceRequested() => Flow.GoTo(GamePhaseId.Business);
    }

    /// <summary>
    /// 영업. 타이머 종료 → 새 손님 중단, 남은 손님은 결제 대기만 처리(설계서 Q1) → 마지막 손님 퇴장
    /// → 남은 청소가 없으면 바로, 있으면 마감 정리 시간(CampaignConfig.closingCleanupSeconds) 뒤 정산.
    /// 나갈 때 치우지 못한 청소 구역 수를 장부(cleanupsMissed)에 적습니다.
    /// </summary>
    public sealed class BusinessPhase : GamePhase
    {
        public BusinessPhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.Business;

        public float TimeLeft { get; private set; }
        /// <summary>타이머는 끝났고 남은 손님을 기다리는 중</summary>
        public bool IsClosing { get; private set; }
        /// <summary>손님이 모두 나가고 마감 정리 시간이 흐르는 중</summary>
        public bool IsCleaningUp => IsClosing && sinceLastCustomer >= 0f;
        /// <summary>마감 정리 남은 시간(초). 정리 중이 아니면 0.</summary>
        public float CleanupTimeLeft => IsCleaningUp ? Mathf.Max(0f, Context.Campaign.closingCleanupSeconds - sinceLastCustomer) : 0f;

        float sinceLastCustomer = -1f; // 음수 = 아직 손님이 남음
        bool skipCleanup;

        public override void Enter()
        {
            TimeLeft = Context.Campaign.GetDay(Context.Session.Day).businessSeconds;
            IsClosing = false;
            sinceLastCustomer = -1f;
            skipCleanup = false;
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
                Flow.Thief?.StopSpawning();
                Log("영업 시간 종료 (남은 손님 결제 대기)");
            }

            // 도둑을 쫓는 중이면 손님처럼 끝날 때까지 기다림 (카드도둑_설계 3장)
            bool hasCustomers = (Flow.Customers != null && Flow.Customers.HasCustomers) || (Flow.Thief != null && Flow.Thief.IsActive);
            if (hasCustomers) return;
            int pending = Flow.Cleanup?.PendingCount ?? 0;
            if (sinceLastCustomer < 0f)
            {
                sinceLastCustomer = 0f;
                if (pending > 0 && !skipCleanup)
                    Context.Events.RaiseNotice($"손님이 다 갔어요. 마감 정리 {Context.Campaign.closingCleanupSeconds:0}초! (남은 청소 {pending}곳)");
            }
            else sinceLastCustomer += deltaTime;

            if (skipCleanup || CleanupRules.ClosingDone(false, pending, sinceLastCustomer, Context.Campaign.closingCleanupSeconds))
            {
                // 정산으로 갈 때만 감점을 적음 (처음부터 다시 · 타이틀로 나갈 때는 Exit에서 걷어 내기만)
                int missed = Flow.Cleanup?.EndDay() ?? 0;
                Context.Session.Ledger.cleanupsMissed += missed;
                if (missed > 0) Log($"치우지 못한 청소 {missed}곳");
                Flow.GoTo(GamePhaseId.Settlement);
            }
        }

        public override void Exit()
        {
            Flow.Cleanup?.EndDay();
            Flow.Thief?.DismissAll();
        }

        /// <summary>디버그: 영업 즉시 종료 (남은 손님 · 도둑도 내보내고 마감 정리도 건너뜀 → 남은 청소는 감점)</summary>
        public override void OnAdvanceRequested()
        {
            TimeLeft = 0f;
            skipCleanup = true;
            Flow.Customers?.DismissAll();
            Flow.Thief?.DismissAll();
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
            Log($"정산 +{LastTotal} (청소 {ledger.cleanupsDone}곳 · 못 치움 {ledger.cleanupsMissed}곳 · 빠른 계산 {ledger.checkoutsFast}건 · 떠난 손님 {ledger.customersLost}명 · 잡은 도둑 {ledger.thievesCaught}명{(ledger.freePackStolen ? " · 무료 팩 도난" : "")}) → 보유 {Context.Session.Wallet.Money}");
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
            // 도둑을 놓친 날은 무료 팩 없음 (카드도둑_설계 2장)
            bool stolen = Context.Session.Ledger.freePackStolen;
            Context.Session.FreePacksLeft = ThiefRules.FreePacksToday(Context.Economy.freePacksPerDay, stolen);
            if (stolen) Context.Events.RaiseNotice("도둑이 무료 팩을 가져가서 오늘은 무료 팩이 없어요…");
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

        // 가게 앞 인도(타이틀)로 옮기므로 페이드 (DayTransition)
        public override void OnAdvanceRequested() => Flow.GoToFaded(GamePhaseId.Title);
    }
}
