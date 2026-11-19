using System.Collections.Generic;
using CardShop.Core;
using CardShop.Customers;
using CardShop.Interaction;
using CardShop.Logic;
using CardShop.Presentation;
using CardShop.UI;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 진행 담당 (튜토리얼_설계 8-1). 게임 이벤트(GameEvents.Happened · 페이즈 · 잡기)를 신호로 바꿔 TutorialProgress에 넘기고,
    /// 시작된 단계를 TutorialView로 보여 주며, 정지 단계 동안 GameFlow.IsHeld를 켭니다(일시정지 메뉴는 뜨지 않음).
    /// 1일차 Intro 장면(IIntroScript)도 맡습니다: 선물 팩을 놓고, 끝나면 카드 더미를 바인더로 보냅니다.
    /// 씬을 다시 빌드하지 않아도 되게 씬이 열리면 스스로 생깁니다(Install). 진행 상태는 저장하지 않습니다(9장 Q3).
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour, IIntroScript
    {
        [Tooltip("비우면 TutorialDefaults의 기본 대본")]
        [SerializeField] TutorialSequence sequence;
        [Tooltip("가만히 있으면 손 힌트를 띄우기까지 (초)")]
        [SerializeField] float idleHintSeconds = 4f;
        [Tooltip("[안내 다시 보기]로 띄운 말풍선이 보이는 시간 (초)")]
        [SerializeField] float replaySeconds = 5f;
        [Tooltip("엉뚱한 물건을 잡았을 때: 알려 주기(기본) / 잠그기")]
        [SerializeField] OutsideGrabMode outsideGrab = OutsideGrabMode.Notify;
        [Tooltip("카운터 메모 자리 (월드)")]
        [SerializeField] Vector3 memoPosition = new Vector3(0.45f, ShopLayout.CounterTop + 0.002f, 0.55f);

        GameContext ctx;
        GameFlow flow;
        TutorialProgress progress;
        List<string> memos;
        TutorialView view;
        IdleHint idle;
        CounterMemo memo;
        GiftPackSpawner gift;
        ThiefDirector thief;
        CardRevealer revealer;
        BinderView binder;
        CheckoutCounter counter;
        readonly TutorialGrabWatcher grabs = new TutorialGrabWatcher();
        readonly List<TutorialSignal> grabSignals = new List<TutorialSignal>();
        System.Func<Transform, bool> isTarget;
        readonly Dictionary<string, string> values = new Dictionary<string, string>();

        bool sessionOn = true;      // 이번 판에 튜토리얼을 쓰는가 (새 게임 때 TutorialSwitch 값)
        bool enteredPending;        // 페이즈가 바뀌었고, 화면 전환이 끝나면 PhaseEntered를 보냄
        bool bonusFreePack;         // 선물 팩을 못 받음 → 1일차 퇴근 후 무료 팩 +1 (9장 Q5)
        bool hintShown, wrongSaid, visible;
        float replayUntil = -1f;
        TutorialLightDim dim;
        InteractionLock locker;
        readonly List<Transform> keep = new List<Transform>();
        readonly TutorialHomeWatch home = new TutorialHomeWatch();

        public bool IntroDone => !sessionOn || progress == null || progress.Skipped || !progress.HasPendingIn(GamePhaseId.Intro, Day);
        int Day => ctx != null && ctx.Session != null ? ctx.Session.Day : 1;
        bool Running => sessionOn && progress != null && !progress.Skipped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (GameContext.Instance == null || FindAnyObjectByType<TutorialDirector>() != null) return;
            new GameObject("Tutorial").AddComponent<TutorialDirector>();
        }

        void Start()
        {
            ctx = GameContext.Instance;
            flow = ctx != null ? ctx.GetComponent<GameFlow>() : null;
            if (flow == null) { enabled = false; return; }

            progress = new TutorialProgress(sequence != null ? sequence.steps : TutorialDefaults.Steps());
            memos = sequence != null ? sequence.memoByDay : TutorialDefaults.Memos();
            progress.Started += OnStarted;
            progress.Ended += s => view.Complete(s); // OnEndedOrCanceled의 Hide보다 먼저 (✓를 잠깐 보여 줌)
            progress.Ended += OnEndedOrCanceled;
            progress.Canceled += OnEndedOrCanceled;

            revealer = FindAnyObjectByType<CardRevealer>();
            thief = FindAnyObjectByType<ThiefDirector>();
            binder = FindAnyObjectByType<BinderView>(FindObjectsInactive.Include);
            counter = FindAnyObjectByType<CheckoutCounter>();
            var font = revealer != null ? revealer.Font : null;

            view = gameObject.AddComponent<TutorialView>();
            isTarget = view.IsTarget;
            view.Configure(font);
            idle = gameObject.AddComponent<IdleHint>();
            dim = gameObject.AddComponent<TutorialLightDim>();
            locker = gameObject.AddComponent<InteractionLock>();
            memo = CounterMemo.Create(transform, font);
            memo.Place(new Pose(memoPosition, Quaternion.LookRotation(Vector3.down, Vector3.forward)));
            memo.Hide();
            gift = gameObject.AddComponent<GiftPackSpawner>();
            gift.Configure(FindAnyObjectByType<ShelfController>(), thief);
            gameObject.AddComponent<PauseMenuTutorialButtons>().Configure(
                FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include), SkipAll, Replay, () => Running);
            gameObject.AddComponent<TutorialTitleToggle>().Configure(FindAnyObjectByType<TitleBoard>(FindObjectsInactive.Include));

            TutorialTargets.MarkFixed();
            FillValues();
            flow.Intro = this;
            ctx.Events.SessionStarted += OnSessionStarted;
            ctx.Events.PhaseChanged += OnPhaseChanged;
            ctx.Events.Happened += OnHappened;
            OnSessionStarted(ctx.Session);
            OnPhaseChanged(GamePhaseId.None, flow.CurrentPhase);
        }

        void OnDestroy()
        {
            if (flow != null) { if (ReferenceEquals(flow.Intro, this)) flow.Intro = null; flow.IsHeld = false; }
            if (ctx == null) return;
            ctx.Events.SessionStarted -= OnSessionStarted;
            ctx.Events.PhaseChanged -= OnPhaseChanged;
            ctx.Events.Happened -= OnHappened;
        }

        void FillValues()
        {
            values["packPrice"] = MoneyText.Format(ctx.Economy.packPrice);
            values["boxPrice"] = MoneyText.Format(ctx.Economy.boxPrice);
            values["cleanupBonus"] = ctx.Economy.cleanupBonus.ToString();
            values["cleanupPenalty"] = ctx.Economy.cleanupPenalty.ToString();
            values["closingCleanupSeconds"] = ctx.Campaign.closingCleanupSeconds.ToString("0");
        }

        // ------------------------------------------------------------------ 게임 이벤트 → 신호

        void OnSessionStarted(GameSession _)
        {
            progress.Reset();
            gift.ResetForNewGame();
            sessionOn = TutorialSwitch.Enabled;
            bonusFreePack = !sessionOn; // 안내를 끄면 선물 팩도 없으므로 1일차 무료 팩 +1
            if (!sessionOn) progress.SkipAll();
        }

        void OnPhaseChanged(GamePhaseId from, GamePhaseId to)
        {
            progress.SetContext(to, Day);
            enteredPending = true;
            if (from == GamePhaseId.Intro) FinishIntro();
            if (to == GamePhaseId.Business && Running && !progress.IsDone(TutorialDefaults.PayHoldKey) && counter != null)
                counter.ForceNextItemCount = 1; // 튜토리얼 첫 손님은 물건 1개 (9장 Q2)
            if (to == GamePhaseId.Title || to == GamePhaseId.Home) memo.Hide();
            home.Reset();
        }

        // 화면 전환이 끝난 첫 프레임: 페이즈 시작 신호 + 그 페이즈의 준비
        void OnEntered(GamePhaseId phase)
        {
            grabs.Refresh();
            if (phase == GamePhaseId.Intro && Running) { gift.Spawn(); if (gift.Pack == null) SkipAll(); } // 선물 팩을 못 놓으면 Intro가 끝나지 않으므로 건너뜀
            if (phase == GamePhaseId.Intro || phase == GamePhaseId.Business) memo.Show(Memo(Day));
            if (phase == GamePhaseId.AfterHours && bonusFreePack && Day == 1)
            {
                bonusFreePack = false;
                ctx.Session.FreePacksLeft++;
            }
            progress.Notify(TutorialSignal.PhaseEntered);
            if (phase == GamePhaseId.AfterHours) CheckBoxAffordable();
        }

        string Memo(int day) => day >= 1 && day <= memos.Count ? memos[day - 1] : null;

        void FinishIntro()
        {
            if (gift.Remove()) bonusFreePack = true;
            if (revealer != null && revealer.HasPile && !revealer.IsBusy) revealer.StashPile(); // 계산대를 비우고 영업 (F4)
        }

        void OnHappened(GameMoment m)
        {
            TutorialSignal s = TutorialSignals.From(m);
            if (s == TutorialSignal.None) return;
            progress.Notify(s);
            if (s == TutorialSignal.RevealFinished && flow.CurrentPhase == GamePhaseId.AfterHours) CheckBoxAffordable();
        }

        void CheckBoxAffordable()
        {
            if (ctx.Session.Wallet.Money >= ctx.Economy.boxPrice) progress.Notify(TutorialSignal.BoxAffordable);
        }

        // ------------------------------------------------------------------ 매 프레임

        void Update()
        {
            bool waiting = flow.IsTransitioning || flow.IsPaused || (flow.Transition != null && flow.Transition.IsSpeaking); // 퇴근 · 아침 혼잣말과 겹치지 않게 (A3)
            if (!waiting && enteredPending)
            {
                enteredPending = false;
                OnEntered(flow.CurrentPhase);
            }

            flow.IsHeld = Running && progress.NeedsHold; // 정지 단계 동안만 게임 시간이 멈춤 (8-3)
            dim.Dimmed = flow.IsHeld; // 정지 중에만 조명을 살짝 낮춤
            SetVisible(!waiting && (progress.Active != null || Time.unscaledTime < replayUntil));
            if (waiting || !Running) return;

            progress.Tick(Time.unscaledDeltaTime);
            WatchGrabs();
            WatchHome();
            UpdateIdleHint();
        }

        // 일시정지 · 화면 전환 중에는 숨겼다가, 돌아오면 지금 단계를 처음부터 다시 띄움 (7장 "헤드셋을 벗음")
        void SetVisible(bool show)
        {
            if (show == visible) return;
            visible = show;
            if (!show) { view.Hide(); return; }
            TutorialStep s = progress.Active ?? progress.LastShown;
            if (s != null) ShowStep(s);
        }

        void WatchGrabs()
        {
            TutorialStep s = progress.Active;
            bool checkWrong = s != null && s.kind != TutorialStepKind.Say;
            bool wrong = grabs.Scan(grabSignals, checkWrong ? isTarget : null);
            foreach (TutorialSignal g in grabSignals) progress.Notify(g);
            // 막지 않고 알려 주기 (확정 결정): 한 단계에 한 번만 말하고 대상을 깜빡임
            if (!wrong || wrongSaid || s == null) return;
            wrongSaid = true;
            view.ChangeText(Format(s.line) + "\n<size=80%><color=#FFD27F>그건 나중에! 지금은 이것부터.</color></size>");
            view.Flash();
        }

        void WatchHome()
        {
            if (flow.CurrentPhase != GamePhaseId.Home) return;
            TutorialSignal s = home.Tick(binder, ctx, Time.unscaledDeltaTime);
            if (s != TutorialSignal.None) progress.Notify(s);
        }

        void UpdateIdleHint()
        {
            TutorialStep s = progress.Active;
            if (s == null || hintShown || s.kind == TutorialStepKind.Say || string.IsNullOrEmpty(s.idleHint)) return;
            if (!idle.IsIdle(idleHintSeconds)) return;
            hintShown = true;
            view.Nudge(); // 손 힌트는 자막의 할 일 줄에 이미 있음 (FIX B1)
        }

        // ------------------------------------------------------------------ 단계 보여 주기

        void OnStarted(TutorialStep s)
        {
            hintShown = wrongSaid = false;
            idle.Restart();
            grabs.Refresh();
            TutorialTargets.MarkDynamic(thief);
            replayUntil = -1f;
            if (visible) ShowStep(s);
        }

        void OnEndedOrCanceled(TutorialStep s) { locker.Unlock(); if (progress.Active == null && Time.unscaledTime >= replayUntil) view.Hide(); }

        void ShowStep(TutorialStep s)
        {
            view.Show(s, Format(s.line));
            if (outsideGrab != OutsideGrabMode.Lock || s != progress.Active || s.kind == TutorialStepKind.Say) return;
            view.CollectTargets(keep);
            locker.Lock(keep); // 단계가 끝나거나 건너뛰면 Unlock
        }

        string Format(string line) => TutorialLineFormatter.Format(line, values);

        // ------------------------------------------------------------------ 일시정지 메뉴

        /// <summary>[안내 건너뛰기]: 남은 안내를 모두 끝내고 정지를 풉니다. Intro 중이면 바로 영업으로 (선물 팩은 무료 팩 +1).</summary>
        public void SkipAll()
        {
            progress.SkipAll();
            flow.IsHeld = false;
            locker.Unlock();
            replayUntil = -1f;
            view.Hide(now: true);
            memo.Hide();
            if (counter != null) counter.ForceNextItemCount = 0;
        }

        /// <summary>[안내 다시 보기]: 마지막 안내를 정지 없이 잠깐 다시 띄웁니다 (메뉴가 닫힌 뒤 보임).</summary>
        public void Replay()
        {
            TutorialStep s = progress.Replay();
            if (s == null) return;
            replayUntil = Time.unscaledTime + replaySeconds + 3f; // 메뉴의 3·2·1 카운트다운만큼 더
            visible = false; // 다음 SetVisible(true)에서 다시 그림
        }
    }
}
