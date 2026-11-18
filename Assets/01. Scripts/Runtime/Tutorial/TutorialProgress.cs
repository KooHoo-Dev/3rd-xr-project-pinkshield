using System;
using System.Collections.Generic;
using CardShop.Core;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 진행 상태 (순수 C#, 튜토리얼_설계 7장 · 8-1).
    /// 단계마다 상태는 대기 / 진행 중 / 끝 셋 중 하나이고, 진행 중은 한 번에 하나뿐입니다.
    /// 바깥(TutorialDirector)에는 Started / Ended / Canceled 이벤트와 NeedsHold만 알려 줍니다.
    ///
    /// 규칙
    /// - 시작 가능 = 아직 안 끝남 + 지금 페이즈와 같음 + 지금 일차가 범위 안 + (after가 비었거나 after 단계가 끝남).
    ///   after가 없는 key를 가리키면 영영 시작하지 않습니다(예외를 던지지 않음).
    /// - startOn 신호가 오면 시작 가능한 단계를 목록 순서로 시작합니다. 이미 진행 중이면 대기열에 넣습니다.
    /// - startOn이 None이면 after 단계가 끝나는 순간에만 시작합니다. after도 비어 있으면 스스로는 절대 시작하지 않습니다.
    /// - 시작하려는 순간 doneOn 신호를 이미 받았으면(플레이어가 먼저 함) Started · Ended 없이 조용히 끝으로 칩니다.
    /// - doneOn 신호가 오면 끝 요청. 최소 표시 시간(seconds)이 지났으면 바로, 아니면 Tick에서 시간이 차면 끝납니다.
    /// - doneOn이 None이면 seconds 뒤 끝. timeout이 0보다 크면 그 시간이 지나도 끝납니다.
    /// - 페이즈 · 일차가 바뀌면 진행 중 단계는 취소(Canceled, 다시 대기로)되고 대기열 · 받은 신호 기록이 지워집니다.
    /// </summary>
    public sealed class TutorialProgress
    {
        readonly IReadOnlyList<TutorialStep> steps;
        readonly bool[] done;
        readonly Dictionary<string, int> indexByKey = new Dictionary<string, int>();
        readonly List<int> ready = new List<int>();               // 시작 신호는 왔지만 다른 단계가 진행 중이라 기다리는 단계
        readonly HashSet<TutorialSignal> seen = new HashSet<TutorialSignal>(); // 지금 페이즈 · 일차에서 받은 신호

        int activeIndex = -1;
        bool doneRequested;
        GamePhaseId phase = GamePhaseId.None;
        int day;

        public TutorialStep Active => activeIndex >= 0 ? steps[activeIndex] : null;
        public TutorialStep LastShown { get; private set; }
        public bool Skipped { get; private set; }
        public bool NeedsHold => Active != null && Active.kind == TutorialStepKind.Hold;
        public float ActiveElapsed { get; private set; }

        public event Action<TutorialStep> Started;
        public event Action<TutorialStep> Ended;     // 끝까지 진행됨
        public event Action<TutorialStep> Canceled;  // 페이즈 · 일차 변경으로 잘림. 대기로 돌아가 다음에 다시 나옴

        public TutorialProgress(IReadOnlyList<TutorialStep> steps)
        {
            this.steps = steps ?? Array.Empty<TutorialStep>();
            done = new bool[this.steps.Count];
            for (int i = 0; i < this.steps.Count; i++)
            {
                TutorialStep s = this.steps[i];
                if (s == null || string.IsNullOrEmpty(s.key)) continue;
                if (!indexByKey.ContainsKey(s.key)) indexByKey.Add(s.key, i); // key가 겹치면 앞의 것
            }
        }

        /// <summary>지금 페이즈 · 일차를 알려 줌. 값이 같으면 아무것도 안 함.</summary>
        public void SetContext(GamePhaseId phase, int day)
        {
            if (phase == this.phase && day == this.day) return;
            this.phase = phase;
            this.day = day;
            ready.Clear();
            seen.Clear();
            if (activeIndex >= 0)
            {
                TutorialStep canceled = steps[activeIndex];
                ClearActive();
                Canceled?.Invoke(canceled);
            }
        }

        /// <summary>게임에서 일어난 일을 알려 줌. 건너뛴 뒤에는 무시합니다.</summary>
        public void Notify(TutorialSignal signal)
        {
            if (Skipped || signal == TutorialSignal.None) return;
            seen.Add(signal);

            // 진행 중 단계의 끝 신호
            if (activeIndex >= 0 && steps[activeIndex].doneOn == signal)
            {
                doneRequested = true;
                if (ActiveElapsed >= steps[activeIndex].seconds) EndActive();
            }

            // 이 신호로 시작하는 단계들 (목록 순서)
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] == null || steps[i].startOn != signal || !IsEligible(i)) continue;
                if (activeIndex < 0) StartStep(i);
                else Enqueue(i);
            }
        }

        /// <summary>매 프레임 시간 진행. 할당 없음.</summary>
        public void Tick(float deltaTime)
        {
            if (activeIndex < 0) return;
            ActiveElapsed += deltaTime;
            TutorialStep s = steps[activeIndex];
            bool byTime = s.doneOn == TutorialSignal.None && ActiveElapsed >= s.seconds;
            bool bySignal = doneRequested && ActiveElapsed >= s.seconds;
            bool byTimeout = s.timeout > 0f && ActiveElapsed >= s.timeout;
            if (byTime || bySignal || byTimeout) EndActive();
        }

        /// <summary>남은 단계를 모두 끝으로 표시. 진행 중 단계만 Ended를 알립니다.</summary>
        public void SkipAll()
        {
            for (int i = 0; i < done.Length; i++) done[i] = true;
            ready.Clear();
            Skipped = true;
            if (activeIndex >= 0)
            {
                TutorialStep ended = steps[activeIndex];
                ClearActive();
                Ended?.Invoke(ended);
            }
        }

        /// <summary>가장 최근에 시작한 단계를 돌려줌. 상태는 바꾸지 않습니다.</summary>
        public TutorialStep Replay() => LastShown;

        /// <summary>새 게임: 모두 대기로, 건너뛰기 해제. 진행 중 단계가 있으면 Canceled를 알립니다. 페이즈 · 일차는 그대로.</summary>
        public void Reset()
        {
            TutorialStep canceled = Active;
            ClearActive();
            Array.Clear(done, 0, done.Length);
            ready.Clear();
            seen.Clear();
            LastShown = null;
            Skipped = false;
            if (canceled != null) Canceled?.Invoke(canceled);
        }

        public bool IsDone(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return indexByKey.TryGetValue(key, out int i) && done[i];
        }

        /// <summary>마지막 페이즈 · 일차 변경 뒤에 이 신호를 받았는가.</summary>
        public bool HasSeen(TutorialSignal signal) => seen.Contains(signal);

        /// <summary>이 페이즈 · 일차에 아직 안 끝난 단계가 있는가 (진행 중 포함).</summary>
        public bool HasPendingIn(GamePhaseId phase, int day)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                TutorialStep s = steps[i];
                if (s != null && !done[i] && s.phase == phase && s.InDay(day)) return true;
            }
            return false;
        }

        bool IsEligible(int i)
        {
            TutorialStep s = steps[i];
            if (s == null || done[i] || i == activeIndex) return false;
            if (s.phase != phase || !s.InDay(day)) return false;
            return string.IsNullOrEmpty(s.after) || IsDone(s.after);
        }

        void Enqueue(int i)
        {
            if (!ready.Contains(i)) ready.Add(i);
        }

        void StartStep(int i)
        {
            ready.Remove(i);
            TutorialStep s = steps[i];
            if (s.doneOn != TutorialSignal.None && HasSeen(s.doneOn))
            {
                // 플레이어가 먼저 해 버림: 보여 주지 않고 끝으로 침
                done[i] = true;
                Cascade(i);
                return;
            }
            activeIndex = i;
            ActiveElapsed = 0f;
            doneRequested = false;
            LastShown = s;
            Started?.Invoke(s);
        }

        void EndActive()
        {
            int i = activeIndex;
            TutorialStep s = steps[i];
            done[i] = true;
            ClearActive();
            Ended?.Invoke(s);
            Cascade(i);
        }

        /// <summary>끝난 단계 뒤를 잇기: after로 이어진 startOn=None 단계 먼저, 없으면 대기열.</summary>
        void Cascade(int endedIndex)
        {
            string key = steps[endedIndex].key;
            if (!string.IsNullOrEmpty(key))
            {
                for (int j = 0; j < steps.Count; j++)
                {
                    TutorialStep s = steps[j];
                    if (s == null || s.startOn != TutorialSignal.None || s.after != key || !IsEligible(j)) continue;
                    if (activeIndex < 0) StartStep(j);
                    else Enqueue(j);
                }
            }

            while (activeIndex < 0 && ready.Count > 0)
            {
                int next = ready[0];
                ready.RemoveAt(0);
                if (IsEligible(next)) StartStep(next);
            }
        }

        void ClearActive()
        {
            activeIndex = -1;
            ActiveElapsed = 0f;
            doneRequested = false;
        }
    }
}
