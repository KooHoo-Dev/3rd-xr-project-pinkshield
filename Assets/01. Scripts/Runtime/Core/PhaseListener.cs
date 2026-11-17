using CardShop.Logic;
using UnityEngine;

namespace CardShop.Core
{
    /// <summary>
    /// GameContext 이벤트(페이즈 · 일차 · 새 게임)를 받는 컴포넌트의 공통 부분. 인테리어 소품(팻말 · 시계 · 달력)과 집 패널(HomePanel)이 씁니다.
    /// 프리팹은 씬 오브젝트를 직접 참조할 수 없어 GameContext.Instance로 찾고, 생성 순서 때문에 OnEnable에서 못 찾으면 Start에서 다시 찾습니다.
    /// </summary>
    public abstract class PhaseListener : MonoBehaviour
    {
        protected GameContext Context { get; private set; }
        protected GameFlow Flow { get; private set; }
        protected GamePhaseId CurrentPhase => Flow != null ? Flow.CurrentPhase : GamePhaseId.None;

        protected virtual void OnEnable() => TrySubscribe();
        protected virtual void Start() => TrySubscribe();

        protected virtual void OnDisable()
        {
            if (Context == null) return;
            Context.Events.PhaseChanged -= HandlePhase;
            Context.Events.DayStarted -= OnDayStarted;
            Context.Events.SessionStarted -= Bind;
            Bind(null);
            Context = null;
            Flow = null;
        }

        void TrySubscribe()
        {
            if (Context != null || GameContext.Instance == null) return;
            Context = GameContext.Instance;
            Flow = Context.GetComponent<GameFlow>();
            Context.Events.PhaseChanged += HandlePhase;
            Context.Events.DayStarted += OnDayStarted;
            Context.Events.SessionStarted += Bind;
            Bind(Context.Session);
            Refresh();
        }

        void HandlePhase(GamePhaseId from, GamePhaseId to) => OnPhaseChanged(to);

        protected virtual void OnPhaseChanged(GamePhaseId phase) => Refresh();
        protected virtual void OnDayStarted(int day) => Refresh();
        /// <summary>구독할 때 · 새 게임마다 지금 세션으로, 구독을 끊을 때 null로 불립니다 (세션 안의 이벤트를 다시 연결하는 곳).</summary>
        protected virtual void Bind(GameSession session) { }
        /// <summary>지금 상태로 다시 그림</summary>
        protected abstract void Refresh();
    }
}
