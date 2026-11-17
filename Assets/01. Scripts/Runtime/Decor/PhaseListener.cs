using CardShop.Core;
using UnityEngine;

namespace CardShop.Decor
{
    /// <summary>
    /// 인테리어 프리팹 안의 소품(팻말 · 시계 · 달력)이 GameContext 이벤트를 받는 공통 부분.
    /// 프리팹은 씬 오브젝트를 직접 참조할 수 없어 GameContext.Instance로 찾고, 생성 순서 때문에 OnEnable에서 못 찾으면 Start에서 다시 찾습니다.
    /// </summary>
    public abstract class PhaseListener : MonoBehaviour
    {
        protected GameContext Context { get; private set; }

        protected virtual void OnEnable() => TrySubscribe();
        protected virtual void Start() => TrySubscribe();

        protected virtual void OnDisable()
        {
            if (Context == null) return;
            Context.Events.PhaseChanged -= HandlePhase;
            Context.Events.DayStarted -= OnDayStarted;
            Context = null;
        }

        void TrySubscribe()
        {
            if (Context != null || GameContext.Instance == null) return;
            Context = GameContext.Instance;
            Context.Events.PhaseChanged += HandlePhase;
            Context.Events.DayStarted += OnDayStarted;
            Refresh();
        }

        void HandlePhase(GamePhaseId from, GamePhaseId to) => OnPhaseChanged(to);

        protected GamePhaseId CurrentPhase
        {
            get
            {
                if (Context == null) return GamePhaseId.None;
                var flow = Context.GetComponent<GameFlow>();
                return flow != null ? flow.CurrentPhase : GamePhaseId.None;
            }
        }

        protected virtual void OnPhaseChanged(GamePhaseId phase) => Refresh();
        protected virtual void OnDayStarted(int day) => Refresh();
        /// <summary>지금 상태로 다시 그림</summary>
        protected abstract void Refresh();
    }
}
