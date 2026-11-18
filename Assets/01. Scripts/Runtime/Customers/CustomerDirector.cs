using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using CardShop.Presentation;
using TMPro;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 영업 중 손님 흐름 (설계서 7-4, Q1 · Q2 · Q10 / 타이틀연출_손님_설계 3장).
    /// 등장 일정 → 문 안(입장 대사) → 진열대 구경(구매 자리 · 대기 줄) → 계산대 줄 → 계산(CheckoutCounter) 또는 인내 시간 초과 → 퇴장.
    /// 이 클래스는 순서 · 대사 · 이벤트만 맡고, 일은 부품이 합니다 (AI-005 · 큰_클래스_나누기_제안 2장):
    ///   <see cref="SpawnClock"/> 등장 시각 · <see cref="CustomerBodyFactory"/> 손님 몸 · <see cref="BrowseArea"/> 구매 자리 + 대기 줄 ·
    ///   <see cref="CheckoutLine"/> 계산대 줄(인내 시간 · 계산 · 떠나기) · <see cref="CustomerRoutes"/> 걷는 길 · CustomerLines 말풍선 대사.
    ///
    /// 손님 동선 (11-19 플레이 피드백, 사진 기준):
    /// - 진열대 뒤(창 쪽) 구매 자리 2칸에서 플레이어 쪽을 보고 구경. 자리가 차면 그 오른쪽으로 창을 따라 대기 줄.
    /// - 다 본 손님은 진열대 왼쪽 끝을 돌아 진열대 앞을 지나 계산대 줄 끝으로.
    /// - 입장 대사는 생성 즉시가 아니라 문 안에 들어왔을 때 (생성 지점은 벽 뒤라 말풍선이 보이지 않았음).
    /// 줄 규칙은 QueueMover 설명 참고 (떠나는 손님은 걸어 나가기 시작할 때까지 칸을 차지).
    /// </summary>
    public sealed class CustomerDirector : MonoBehaviour, ICustomerFloor
    {
        [SerializeField] GameFlow flow;
        [SerializeField] CustomerRoster roster;

        [Header("동선 (CustomerFloorBuilder가 배치)")]
        [Tooltip("매장 밖 등장 · 퇴장 지점")]
        [SerializeField] Transform outside;
        [Tooltip("출입문 바로 바깥. 손님은 직선으로 걸으므로 밖 → 이 지점 → 문 순서로 지나가야 벽을 뚫지 않음 (비우면 밖 → 문)")]
        [SerializeField] Transform doorOutside;
        [Tooltip("출입문. 들어오고 나갈 때 지나감")]
        [SerializeField] Transform door;
        [Tooltip("입장 통로가 꺾이는 곳 (카드 도둑 길). 비우면 문")]
        [SerializeField] Transform entryTurn;
        [Tooltip("진열대 옆 통로의 아래 끝 (카운터 앞 낮은 통로). x = 통로 x")]
        [SerializeField] Transform aisleLow;
        [Tooltip("결제한 손님이 줄을 가로지르지 않고 빠지는 카운터 오른쪽 지점")]
        [SerializeField] Transform counterExit;
        [Tooltip("진열대 뒤 구매 자리. forward = 진열대(플레이어) 쪽")]
        [SerializeField] Transform[] browseSpots;
        [Tooltip("구매 자리 대기 줄 (0번이 맨 앞). 비우면 ShopLayout.BrowseWaitPositions")]
        [SerializeField] Transform[] browseWaitSlots;
        [Tooltip("계산대 줄. 0번이 맨 앞(결제 자리), forward = 플레이어 쪽")]
        [SerializeField] Transform[] queueSlots;

        [Header("계산")]
        [Tooltip("장바구니 · 바코드 · 포스기 · 결제를 맡는 계산대")]
        [SerializeField] CheckoutCounter counter;

        [Header("말풍선")]
        [Tooltip("손님 대사 목록 (비우면 코드의 기본 문장)")]
        [SerializeField] CustomerLineSet lineSet;
        [SerializeField] TMP_FontAsset bubbleFont;

        [Header("연출 시간(초)")]
        [SerializeField] float reactionSeconds = 1.6f;
        [SerializeField] float angrySeconds = 1.8f;

        GameContext context;
        CustomerLines lines;
        CustomerBodyFactory bodies;
        BrowseArea browse;
        CheckoutLine checkout;
        readonly SpawnClock clock = new SpawnClock();
        readonly List<CustomerAgent> agents = new List<CustomerAgent>();

        public bool IsPaused => flow != null && flow.IsPaused;
        public bool HasCustomers => agents.Count > 0 || clock.HasPending;
        /// <summary>지금 매장 안팎에 있는 손님 (입구 종 등 연출이 읽기만 함)</summary>
        public IReadOnlyList<CustomerAgent> Agents => agents;
        /// <summary>출입문 지점 (입구 종이 문을 지나는지 판단할 때 사용)</summary>
        public Transform Door => door;

        /// <summary>손님이 걷는 통로 (도둑도 같은 통로를 씀)</summary>
        public CustomerRoutes Routes => new CustomerRoutes(
            outside.position,
            doorOutside != null ? doorOutside.position : door.position,
            door.position,
            entryTurn != null ? entryTurn.position : door.position,
            aisleLow != null ? aisleLow.position : door.position,
            counterExit != null ? counterExit.position : door.position);

        void Awake()
        {
            bodies = new CustomerBodyFactory(transform, () => outside, () => bubbleFont, () => IsPaused);
            browse = new BrowseArea(browseSpots.Length, SpotPose, WaitPose, () => Routes, StartBrowse);
            checkout = new CheckoutLine(SlotPose, counter, new CheckoutLine.Hooks
            {
                Context = () => context,
                LineSet = () => lineSet,
                Say = Say,
                Chance = Chance,
                LeavePath = LeavePath,
                Left = a => { context.Events.RaiseCustomerLeft(); Remove(a); },
                ReactionSeconds = () => reactionSeconds,
                AngrySeconds = () => angrySeconds,
            });
        }

        void OnEnable()
        {
            if (flow == null) return;
            context = flow.GetComponent<GameContext>();
            flow.Customers = this;
            if (context != null) context.Events.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            if (flow != null && ReferenceEquals(flow.Customers, this)) flow.Customers = null;
            if (context != null) context.Events.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(GamePhaseId from, GamePhaseId to)
        {
            if (to == GamePhaseId.Business) BeginDay();
            else if (from == GamePhaseId.Business) { clock.Stop(); DismissAll(); }
        }

        void BeginDay()
        {
            DismissAll();
            clock.Start(null);
            if (!bodies.BeginDay(roster, context.Random))
            {
                Debug.LogWarning("[CustomerDirector] CustomerRoster가 비어 있어 손님이 오지 않습니다. Tools > CardShop > Customers > Build Customer Roster", this);
                return;
            }
            lines ??= new CustomerLines(lineSet != null ? lineSet.lines : null, context.Random);

            CampaignConfig.DayEntry day = context.Campaign.GetDay(context.Session.Day);
            clock.Start(CustomerSchedule.Build(day, context.Campaign.spawnWindowRatio, context.Random, context.Campaign.Waves));
        }

        void Update()
        {
            if (IsPaused) return;
            float dt = Time.deltaTime;

            // 한 프레임에 여러 명 차례가 될 수 있음 (처음 한 번만 시간을 흘림)
            while (clock.Tick(dt))
            {
                dt = 0f;
                Spawn();
            }

            checkout.Tick(); // 인내 시간 대사 · 초과하면 화내고 떠남
        }

        // ------------------------------------------------------------------ 손님 흐름

        void Spawn()
        {
            CustomerAgent agent = bodies.Create("Customer");
            if (agent == null) return;
            agents.Add(agent);
            agent.State = CustomerState.Enter;
            agent.WalkPath(Routes.EnterToDoor(), null, () => ArriveAtDoor(agent));
        }

        // 문 안에 들어옴: 인사 + 입장 대사 (11-19 피드백: 생성 지점은 벽 뒤라 대사가 다 지나간 뒤에야 보였음) → 구매 자리 또는 대기 줄
        void ArriveAtDoor(CustomerAgent agent)
        {
            agent.Gesture(CustomerAnim.Wave);
            if (Chance(lineSet != null ? lineSet.enterChance : 0.5f)) Say(agent, CustomerMoment.Enter);
            browse.Enter(agent, context != null ? context.Random : null);
        }

        /// <summary>
        /// 손님 외형으로 손님이 아닌 인물(도둑)을 밖 지점에 만듭니다. 손님 목록 · 줄에는 들어가지 않으므로
        /// 없애는 일은 만든 쪽이 맡습니다. 오늘 손님 외형을 아직 못 정했으면(영업 전) null.
        /// </summary>
        public CustomerAgent SpawnExtra(string prefix) => bodies != null ? bodies.Create(prefix) : null;

        /// <summary>
        /// 도둑을 잡음 (카드도둑_설계 2장 영웅 보너스): 줄 선 손님이 박수 치고 인내 시간을 bonusSeconds만큼 되돌려 받습니다.
        /// </summary>
        public void CheerThiefCaught(float bonusSeconds)
        {
            checkout.Cheer(bonusSeconds);
            foreach (CustomerAgent a in agents)
                if (a.State != CustomerState.Leave && a.Bubble != null && Chance(0.6f)) a.Bubble.Say(Pick(HeroLines), 2.2f);
        }

        static readonly string[] HeroLines = { "와, 멋있다!", "나이스!", "(짝짝짝)", "알바생 최고!", "잡았다!" };

        string Pick(string[] list) => list[context != null ? context.Random.Range(0, list.Length) : 0];

        void StartBrowse(CustomerAgent agent)
        {
            agent.State = CustomerState.Browse;
            agent.Gesture(CustomerAnim.Question);
            if (Chance(lineSet != null ? lineSet.browseChance : 0.6f)) Say(agent, CustomerMoment.Browse);
            agent.Wait(RandomIn(roster.browseSeconds), () => JoinQueue(agent));
        }

        // range.x ~ range.y 사이 균등 (x > y면 x)
        float RandomIn(Vector2 range) => range.x + (float)context.Random.NextDouble() * Mathf.Max(0f, range.y - range.x);

        bool Chance(float p) => context != null && context.Random.NextDouble() < p;

        // 구경을 마침: 구매 자리를 비우고(대기 손님이 들어옴) 계산대 줄 끝으로.
        // 구매 대기 줄이 없으면 진열대 오른쪽으로 바로, 있으면 대기 줄을 피해 왼쪽으로 돌아감. 계산대 줄이 비었으면 결제 자리로 곧장
        void JoinQueue(CustomerAgent agent)
        {
            Vector3 from = agent.transform.position;
            bool shortcut = browse.Waiting == 0;
            bool lineEmpty = checkout.Members.Count == 0;
            browse.Release(agent);
            checkout.Join(agent, tail => aisleLow != null ? Routes.BrowseToQueue(from, tail, shortcut, lineEmpty) : new[] { tail }); // 예전 씬(통로 지점 없음): 곧장
        }

        // 줄 칸 위치 · 방향. 준비된 칸보다 줄이 길면 마지막 두 칸 간격대로 이어서 섭니다.
        Pose SlotPose(int index) => LinePose(queueSlots, null, index);

        Pose SpotPose(int index) => new Pose(browseSpots[index].position, browseSpots[index].rotation);

        // 대기 줄은 앞사람(왼쪽, 진열대 쪽)을 보고 섬
        Pose WaitPose(int index) => LinePose(browseWaitSlots, ShopLayout.BrowseWaitPositions, index);

        static Pose LinePose(Transform[] slots, Vector3[] fallback, int index)
        {
            Vector3[] points;
            Quaternion rot;
            if (slots != null && slots.Length > 0)
            {
                points = new Vector3[slots.Length];
                for (int i = 0; i < points.Length; i++) points[i] = slots[i].position;
                rot = slots[Mathf.Min(index, slots.Length - 1)].rotation;
            }
            else
            {
                points = fallback;
                rot = Quaternion.LookRotation(Vector3.left);
            }
            Vector3 lastForward = rot * Vector3.forward;
            return new Pose(CustomerRoutes.SlotPosition(points, lastForward, index), rot);
        }

        // 계산대 줄에서 빠진 손님이 문 밖까지 걷는 길: 맨 앞(결제 자리)은 카운터 앞으로, 줄 중간은 줄 뒤 길로
        Vector3[] LeavePath(CustomerAgent agent, int index)
        {
            if (aisleLow == null || counterExit == null) return doorOutside != null
                ? new[] { door.position, doorOutside.position, outside.position }
                : new[] { door.position, outside.position }; // 예전 씬
            CustomerRoutes routes = Routes;
            return index == 0 && agent.InLine ? routes.LeaveFromCounter() : routes.LeaveFromQueue(agent.transform.position, queueSlots[0].position);
        }

        void Say(CustomerAgent agent, CustomerMoment moment, string cardName = null)
        {
            if (agent == null || agent.Bubble == null) return;
            lines ??= new CustomerLines(lineSet != null ? lineSet.lines : null, context != null ? context.Random : null);
            agent.Bubble.Say(lines.Pick(moment, cardName), lineSet != null ? lineSet.bubbleSeconds : 2.8f);
            // 말풍선과 함께 짧은 감탄 소리 (오, 아하, 와우 / 화내기) — SoundBank 칸이 비면 조용
            SoundId voice = moment == CustomerMoment.Angry ? SoundId.CustomerAngry
                : agent.IsFemale ? SoundId.CustomerVoiceFemale : SoundId.CustomerVoiceMale;
            Sfx.Play(voice, agent.Bubble.transform.position);
        }

        void Remove(CustomerAgent agent)
        {
            agents.Remove(agent);
            checkout.Forget(agent);
            if (agent != null) Destroy(agent.gameObject);
        }

        // ------------------------------------------------------------------ ICustomerFloor

        public void StopSpawning() => clock.Stop();

        public void DismissAll()
        {
            checkout.Clear();
            foreach (CustomerAgent a in agents)
                if (a != null) Destroy(a.gameObject);
            agents.Clear();
            browse.Clear();
        }
    }
}
