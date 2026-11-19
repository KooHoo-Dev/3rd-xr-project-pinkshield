using System.Collections;
using CardShop.Cards;
using CardShop.Cleanup;
using CardShop.Core;
using CardShop.Logic;
using CardShop.Presentation;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 카드 도둑 (카드도둑_설계). 3일차부터 하루 한 번(CampaignConfig.thief), 복면 쓴 손님이 계산대 위 "오늘 무료 팩"을 들고 달아납니다.
    ///   등장 → 훔칠 자리에서 머뭇 → 팩을 집음 → 약 올림 → 통로 · 문 · 인도로 달아남
    ///   ├ 빗자루(Broom)에 맞음: 팩을 그 자리에 떨어뜨리고 밖으로 도망. 줄 손님 박수 + 인내 시간 회복(CustomerDirector.CheerThiefCaught).
    ///   │  떨어진 팩을 집으면 계산대로 돌아감. 안 집고 영업이 끝나도 되찾은 것으로 침.
    ///   └ 인도 끝(문에서 escapeDistance) 도착: 장부에 도난 → 퇴근 후 무료 팩 0개 (ThiefRules.FreePacksToday)
    /// 이 클래스는 흐름(등장 → 훔침 → 도망/잡힘)과 맞음 판정 · 말하기만 맡습니다 (AI-005).
    ///   팩이 어디 있나 = <see cref="FreePackState"/>(순수, 테스트됨) · 팩 옮기기 = <see cref="FreePackHolder"/> ·
    ///   등장 시각 = <see cref="SpawnClock"/>(순수, 테스트됨) · 복면 · 키 · 손 = <see cref="ThiefBody"/> ·
    ///   판단(등장 일정 · 맞음 · 길) = ThiefRules · 몸(외형 · 걷기 · 말풍선) = CustomerDirector.SpawnExtra.
    /// 일시정지 중에는 등장 시계 · 맞음 판정이 멈추고, 도둑 걷기는 CustomerAgent가 멈춥니다.
    /// </summary>
    public sealed class ThiefDirector : MonoBehaviour, IThiefFloor
    {
        enum State { None, Approach, Linger, Taunt, Flee, RunOff }

        [SerializeField] GameFlow flow;
        [Tooltip("손님 외형 · 통로를 빌려 씀")]
        [SerializeField] CustomerDirector customers;
        [Tooltip("계산대 위 오늘 무료 팩 (잡기 + HeldItem). 이 자리가 팩이 돌아올 자리")]
        [SerializeField] HeldItem freePack;
        [Tooltip("무료 팩 받침 + 안내 글자 묶음 (영업 중에만 보임). 비워 두면 팩만 켜고 끔")]
        [SerializeField] GameObject display;
        [Tooltip("도둑이 팩을 집으려고 서는 자리. forward = 팩 쪽")]
        [SerializeField] Transform stealSpot;
        [SerializeField] Broom broom;
        [Tooltip("도둑 머리에 씌우는 복면 원본 (꺼 둠)")]
        [SerializeField] GameObject maskTemplate;
        [Tooltip("인도 가운데 z (도둑이 문을 나와 옆으로 달리는 줄)")]
        [SerializeField] float sidewalkZ = 6.4f;
        [Tooltip("복면 가운데를 머리 뼈에서 이만큼 위로 (m, 손님 키 배율 전)")]
        [SerializeField] float maskHeadOffset = 0.09f;
        [Tooltip("바닥에 떨어진 팩 높이 (눕혔을 때 두께의 절반, m)")]
        [SerializeField] float droppedPackHeight = 0.008f;
        [Tooltip("도둑 옷 색 (손님 몸 전체에 곱함). 어두울수록 수상해 보임 (11-19 피드백)")]
        [SerializeField] Color outfitTint = new Color(0.22f, 0.22f, 0.26f);
        [Tooltip("빗자루에 맞은 뒤 휘청하다 도망치기까지 (초)")]
        [SerializeField] float hitStunSeconds = 1.2f;

        static readonly string[] EnterLines = { "(두리번두리번)", "아무도 안 보지…?", "흠흠, 손님입니다~" };
        static readonly string[] StealLines = { "이건 공짜죠? ㅎㅎ", "무료 팩 잘 받아 갑니다~", "오늘 운 좋네!" };
        static readonly string[] CaughtLines = { "으악! 잘못했어요!", "아야! 안 훔쳐요, 안 훔쳐!", "대걸레는 반칙이잖아!" };
        static readonly string[] EscapeLines = { "메롱~", "잡아 보시지!", "다음에 또 올게~" };

        readonly FreePackState pack = new FreePackState();
        readonly SpawnClock clock = new SpawnClock();
        FreePackHolder holder;
        GameContext context;
        CampaignConfig.ThiefSettings settings;
        CustomerAgent thief;
        State state;
        float thiefHeight = 1.7f;
        Vector3 farEscape;

        public bool IsActive => thief != null || (clock.HasPending && pack.AtHome);

        void Awake()
        {
            if (freePack != null) holder = new FreePackHolder(freePack, display); // 만들 때 놓인 자리 = 돌아올 자리
        }

        void OnEnable()
        {
            if (flow == null) return;
            context = flow.GetComponent<GameContext>();
            flow.Thief = this;
            if (context != null) context.Events.PhaseChanged += OnPhaseChanged;
            if (freePack != null)
            {
                freePack.Grabbed += OnPackGrabbed;
                freePack.Released += holder.OnReleased;
            }
        }

        void OnDisable()
        {
            if (flow != null && ReferenceEquals(flow.Thief, this)) flow.Thief = null;
            if (context != null) context.Events.PhaseChanged -= OnPhaseChanged;
            if (freePack != null)
            {
                freePack.Grabbed -= OnPackGrabbed;
                freePack.Released -= holder.OnReleased;
            }
        }

        // 지킬 팩은 영업 중에만 보임 (퇴근 후에는 FreePackOffer가 같은 자리에 열 수 있는 팩을 놓음)
        void Start() => holder?.Show(flow != null && flow.CurrentPhase == GamePhaseId.Business);

        void OnPhaseChanged(GamePhaseId from, GamePhaseId to)
        {
            if (to == GamePhaseId.Business) BeginDay();
            else if (from == GamePhaseId.Business)
            {
                DismissAll();
                holder?.Show(false);
            }
        }

        void BeginDay()
        {
            DismissAll();
            pack.StartDay();
            holder?.ReturnHome();
            holder?.Show(true);

            settings = context.Campaign.thief;
            int day = context.Session.Day;
            int count = ThiefRules.CountToday(day, settings.firstDay, settings.perDay);
            float business = context.Campaign.GetDay(day).businessSeconds;
            clock.Start(ThiefRules.Schedule(count, business, settings.spawnWindow, context.Random));
        }

        void Update()
        {
            if (flow == null || flow.IsTimeStopped || flow.CurrentPhase != GamePhaseId.Business) return;

            // 도둑이 나와 있는 동안은 시계가 멈춤. 훔칠 팩이 없으면(이미 잃었거나 바닥에 있음) 차례가 와도 안 옴
            if (thief == null && clock.Tick(Time.deltaTime) && pack.AtHome) Spawn();

            if (thief != null && CanBeHit && broom != null && broom.IsHeld &&
                ThiefRules.IsHit(broom.HeadPosition, broom.HeadSpeed, thief.transform.position, thiefHeight, settings.hitRadius, settings.hitSpeed))
                Caught();
        }

        /// <summary>지금 나와 있는 도둑 (튜토리얼 하이라이트용). 없으면 null.</summary>
        public Transform ActiveThief => thief != null ? thief.transform : null;
        /// <summary>도둑을 치는 빗자루</summary>
        public Broom Broom => broom;

        bool CanBeHit => state == State.Linger || state == State.Taunt || state == State.Flee;

        // ------------------------------------------------------------------ 흐름

        void Spawn()
        {
            if (customers == null || stealSpot == null || holder == null) return;
            thief = customers.SpawnExtra("Thief");
            if (thief == null) return;

            Transform head = HumanoidHeight.Head(thief.GetComponentInChildren<Animator>());
            thiefHeight = ThiefBody.Height(thief, head);
            ThiefBody.DressDark(thief, outfitTint);
            ThiefBody.WearMask(thief, head, maskTemplate, maskHeadOffset, thiefHeight);

            state = State.Approach;
            context.Events.RaiseHappened(GameMoment.ThiefAppeared);
            Say(EnterLines);
            Sfx.Play(thief.IsFemale ? SoundId.ThiefLaughFemale : SoundId.ThiefLaughMale, thief.transform.position); // 등장 힌트
            thief.WalkPath(ThiefRules.ApproachPath(customers.Routes, stealSpot.position), stealSpot.rotation, () =>
            {
                state = State.Linger;
                thief.Gesture(CustomerAnim.Question);
                thief.Wait(settings.lingerSeconds, Steal);
            });
        }

        void Steal()
        {
            if (thief == null || !pack.Steal()) return;
            holder.AttachTo(ThiefBody.Hand(thief), thief.transform);

            state = State.Taunt;
            thief.Gesture(CustomerAnim.Cheer);
            Sfx.Play(thief.IsFemale ? SoundId.ThiefLaughFemale : SoundId.ThiefLaughMale, thief.transform.position); // 훔친 순간 그 자리에서 (빨간 외곽선 대신 소리로 찾기)
            Say(StealLines);
            context.Events.RaiseNotice("도둑이야! 오늘 무료 팩을 들고 도망가요. 카운터 오른쪽 대걸레로 한 대!");
            thief.Wait(settings.tauntSeconds, Flee);
        }

        void Flee()
        {
            if (thief == null) return;
            state = State.Flee;
            bool left = context.Random.NextDouble() < 0.5;
            CustomerRoutes routes = customers.Routes;
            Vector3 escapePoint = ThiefRules.EscapePoint(routes.DoorOutside, sidewalkZ, settings.escapeDistance, left);
            farEscape = ThiefRules.EscapePoint(routes.DoorOutside, sidewalkZ, settings.escapeDistance + 12f, left);
            thief.WalkSpeed = settings.runSpeed;
            thief.WalkPath(ThiefRules.FleePath(routes, escapePoint), null, Escaped);
        }

        void Escaped()
        {
            if (thief == null) return;
            state = State.RunOff;
            pack.Escaped();
            context.Session.Ledger.freePackStolen = true;
            context.Events.RaiseHappened(GameMoment.ThiefEscaped);
            Say(EscapeLines);
            Sfx.Play(SoundId.ThiefEscaped);
            context.Events.RaiseNotice("도둑을 놓쳤어요… 오늘 퇴근 후 무료 팩은 없어요.");
            thief.WalkPath(new[] { farEscape }, null, RemoveThief);
        }

        void Caught()
        {
            bool hadPack = pack.Caught();
            state = State.RunOff;
            context.Session.Ledger.thievesCaught++;
            context.Events.RaiseHappened(GameMoment.ThiefCaught);
            Sfx.Play(SoundId.BroomHit, thief.transform.position + Vector3.up);
            Sfx.Play(SoundId.ThiefCaught, thief.transform.position);
            thief.Gesture(CustomerAnim.Hit); // 전신 휘청 (Kevin Iglesias Stun01, 11-19 피드백: 맞아도 반응이 없어 어색)
            Say(CaughtLines);

            if (hadPack) holder.DropInFrontOf(thief.transform, droppedPackHeight);
            customers.CheerThiefCaught(settings.heroPatienceBonus);
            context.Events.RaiseNotice(hadPack
                ? $"도둑을 잡았어요! 떨어진 무료 팩을 주워 오세요. (줄 손님 인내 +{settings.heroPatienceBonus:0}초)"
                : $"훔치기 전에 막았어요! (줄 손님 인내 +{settings.heroPatienceBonus:0}초)");

            // 잠깐 멈칫한 뒤 가까운 쪽으로 도망
            thief.WalkTo(thief.transform.position, null, null);
            thief.WalkSpeed = settings.runSpeed;
            thief.Wait(hitStunSeconds, () =>
            {
                if (thief == null) return;
                CustomerRoutes routes = customers.Routes;
                bool left = thief.transform.position.x < routes.DoorOutside.x;
                Vector3 far = ThiefRules.EscapePoint(routes.DoorOutside, sidewalkZ, settings.escapeDistance + 12f, left);
                bool inside = ShopLayout.InsideShop(thief.transform.position);
                thief.WalkPath(ThiefRules.RunOffPath(routes, thief.transform.position, inside, far), null, RemoveThief);
            });
        }

        void RemoveThief()
        {
            Apply(pack.ThiefRemoved()); // 도둑 손의 팩은 도둑과 함께 사라지지 않게 먼저 떼어 냄
            if (thief != null) Destroy(thief.gameObject);
            thief = null;
            state = State.None;
        }

        // ------------------------------------------------------------------ 팩 되찾기

        void OnPackGrabbed(HeldItem _)
        {
            if (!pack.PickUp()) return;
            context.Events.RaiseNotice("무료 팩을 되찾았어요!");
            StartCoroutine(ReturnPackNextFrame());
        }

        // 잡은 그 프레임에 잡기를 끊으면 XRI 선택 처리 도중이라, 한 프레임 뒤에 계산대로 돌려보냄
        IEnumerator ReturnPackNextFrame()
        {
            yield return null;
            holder.ReturnHome();
        }

        void Apply(FreePackState.Move move)
        {
            if (holder == null || move == FreePackState.Move.None) return;
            holder.ReturnHome(hide: move == FreePackState.Move.HomeHidden);
        }

        void Say(string[] lines)
        {
            if (thief == null || thief.Bubble == null || lines.Length == 0) return;
            thief.Bubble.Say(lines[context.Random.Range(0, lines.Length)], 2.4f);
        }

        // ------------------------------------------------------------------ IThiefFloor

        public void StopSpawning() => clock.Stop();

        /// <summary>디버그 치트: 영업 중이고 팩이 제자리면 지금 바로 등장 (일정표와 상관없이). 등장했으면 true.</summary>
        public bool DebugSpawnNow()
        {
            if (flow == null || flow.CurrentPhase != GamePhaseId.Business || thief != null || !pack.AtHome) return false;
            Spawn();
            return thief != null;
        }

        /// <summary>무료 팩 받침 위 자리 (퇴근 후 FreePackOffer가 열 수 있는 팩을 놓는 곳). 무료 팩이 없는 씬이면 false.</summary>
        public bool TryGetFreePackHome(out Pose pose)
        {
            pose = holder != null ? holder.HomePose : default;
            return holder != null;
        }

        /// <summary>받침 · 안내 글자만 켜고 끔 (지킨 금색 팩은 숨김).</summary>
        public void ShowFreePackStand(bool on) => holder?.ShowStandOnly(on);

        public void DismissAll()
        {
            clock.Stop();
            Apply(pack.ForceEnd()); // 도둑 손의 팩은 도난으로 치지 않고, 잡고 안 주운 팩은 되찾은 것으로 침
            if (thief != null) Destroy(thief.gameObject);
            thief = null;
            state = State.None;
        }
    }
}
