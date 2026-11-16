using CardShop.Cards;
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
    /// 영업. 타이머 종료 → (TODO 4일차: 남은 손님 처리 → 마지막 손님 퇴장 후 마감 정리 시간) → 정산.
    /// </summary>
    public sealed class BusinessPhase : GamePhase
    {
        public BusinessPhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.Business;

        public float TimeLeft { get; private set; }

        public override void Enter()
        {
            TimeLeft = Context.Campaign.GetDay(Context.Session.Day).businessSeconds;
            Log($"영업 시작 ({TimeLeft:0}초)");
        }

        public override void Tick(float deltaTime)
        {
            if (Flow.IsPaused) return;
            TimeLeft -= deltaTime;
            if (TimeLeft <= 0f) Flow.GoTo(GamePhaseId.Settlement);
        }

        /// <summary>디버그: 영업 즉시 종료</summary>
        public override void OnAdvanceRequested() => TimeLeft = 0f;
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

        public override void OnAdvanceRequested() => Flow.GoTo(GamePhaseId.DayEnd);
    }

    /// <summary>하루 마무리. TODO: 미판매 진열 카드 반환, 선반 재배치. 마지막 날이면 최종 정산.</summary>
    public sealed class DayEndPhase : GamePhase
    {
        public DayEndPhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.DayEnd;

        public override void Enter()
        {
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

    /// <summary>최종 정산 · 랭크 · 엔딩. TODO(5일차): ScoreRules로 점수 계산.</summary>
    public sealed class FinalPhase : GamePhase
    {
        public FinalPhase(GameFlow f, GameContext c) : base(f, c) { }
        public override GamePhaseId Id => GamePhaseId.Final;

        public override void Enter()
        {
            int score = Context.Session.Wallet.Money; // 임시: 재화만
            ScoreConfig.Rank rank = Context.Score.GetRank(score);
            Log($"최종 {score}점, 랭크 {rank.name}, {(rank.isSuccess ? Context.Score.successEnding : Context.Score.failEnding)}");
        }

        public override void OnAdvanceRequested() => Flow.GoTo(GamePhaseId.Title);
    }
}
