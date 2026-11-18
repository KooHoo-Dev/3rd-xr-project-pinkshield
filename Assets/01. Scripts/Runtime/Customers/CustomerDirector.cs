using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using TMPro;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 영업 중 손님 흐름 (설계서 7-4, Q1 · Q2 · Q10 / 타이틀연출_손님_설계 3장).
    /// 등장 일정 → 입장 → 진열대 구경 → 계산대 줄 → 계산(CheckoutCounter) 또는 인내 시간 초과 → 퇴장.
    /// 외형은 CustomerRoster에서 그룹 가중치로 뽑습니다. 줄 관리 · 이벤트는 여기, 장바구니 · 바코드 · 결제는 CheckoutCounter,
    /// 걷기 · 제스처는 CustomerAgent, 걷는 길(통로)은 CustomerRoutes, 말풍선 대사는 CustomerLines.
    ///
    /// 줄 규칙 (2026-11-18 기기 테스트 피드백: 손님이 줄을 서지 않고 겹쳐 섬):
    /// - 떠나는 손님(결제 후 인사 · 화내기 중)은 걸어 나가기 시작할 때까지 줄 칸을 차지합니다. 그 뒤에 줄이 한 칸씩 당겨집니다.
    /// - 줄에 합류할 때는 통로를 따라 줄 끝 뒤에서 다가갑니다. 걸어오는 중에 줄이 당겨지면 도착한 뒤 줄을 따라 앞으로 갑니다.
    /// - 구경 자리가 다 차 있으면 다른 손님 자리에 겹쳐 서지 않고 바로 줄 끝으로 갑니다.
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
        [Tooltip("입장 통로가 꺾이는 곳 (앞벽을 따라 온 뒤 진열대 옆 통로로 내려감). 비우면 예전처럼 문 → 목적지 직선")]
        [SerializeField] Transform entryTurn;
        [Tooltip("진열대 옆 통로의 아래 끝 (카운터 앞 낮은 통로). x = 통로 x")]
        [SerializeField] Transform aisleLow;
        [Tooltip("결제한 손님이 줄을 가로지르지 않고 빠지는 카운터 오른쪽 지점")]
        [SerializeField] Transform counterExit;
        [Tooltip("진열대 앞 구경 자리. forward = 진열대 쪽")]
        [SerializeField] Transform[] browseSpots;
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
        CustomerPicker picker;
        CustomerLines lines;
        readonly List<CustomerAgent> agents = new List<CustomerAgent>();
        // 계산대 줄. 떠나는 손님도 걸어 나가기 시작할 때까지는 자기 칸을 차지하므로 여기에 남아 있음 (State == Leave)
        readonly List<CustomerAgent> queue = new List<CustomerAgent>();

        // 지금 계산대에 물건을 올린 손님 (줄 맨 앞 한 명뿐)
        CustomerAgent checkoutAgent;

        float[] schedule = new float[0];
        int nextSpawn;
        float elapsed;
        bool spawning;

        public bool IsPaused => flow != null && flow.IsPaused;
        public bool HasCustomers => agents.Count > 0 || (spawning && nextSpawn < schedule.Length);
        /// <summary>지금 매장 안팎에 있는 손님 (입구 종 등 연출이 읽기만 함)</summary>
        public IReadOnlyList<CustomerAgent> Agents => agents;
        /// <summary>출입문 지점 (입구 종이 문을 지나는지 판단할 때 사용)</summary>
        public Transform Door => door;

        CustomerRoutes Routes => new CustomerRoutes(
            outside.position,
            doorOutside != null ? doorOutside.position : door.position,
            door.position,
            entryTurn != null ? entryTurn.position : door.position,
            aisleLow != null ? aisleLow.position : door.position,
            counterExit != null ? counterExit.position : door.position);

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
            else if (from == GamePhaseId.Business) { spawning = false; DismissAll(); }
        }

        void BeginDay()
        {
            DismissAll();
            if (roster == null || roster.entries.Count == 0)
            {
                Debug.LogWarning("[CustomerDirector] CustomerRoster가 비어 있어 손님이 오지 않습니다. Tools > CardShop > Customers > Build Customer Roster", this);
                schedule = new float[0];
                return;
            }

            var looks = new List<CustomerLook>(roster.entries.Count);
            foreach (CustomerRoster.Entry e in roster.entries) looks.Add(e.look);
            picker = new CustomerPicker(looks, roster.localWeight, roster.foreignWeight, roster.noRepeatCount, context.Random);
            lines ??= new CustomerLines(lineSet != null ? lineSet.lines : null, context.Random);

            CampaignConfig.DayEntry day = context.Campaign.GetDay(context.Session.Day);
            schedule = CustomerSchedule.Build(day, context.Campaign.spawnWindowRatio, context.Random, context.Campaign.Waves);
            nextSpawn = 0;
            elapsed = 0f;
            spawning = true;
        }

        void Update()
        {
            if (IsPaused) return;
            float dt = Time.deltaTime;

            if (spawning)
            {
                elapsed += dt;
                while (nextSpawn < schedule.Length && schedule[nextSpawn] <= elapsed)
                {
                    nextSpawn++;
                    Spawn();
                }
            }

            // 인내 시간: 중간에 '기다리는 중' · '재촉' 대사, 넘으면 감점 없이 그냥 떠남 (설계서 Q2)
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                CustomerAgent a = queue[i];
                if (a.State != CustomerState.Queue) continue;
                float patience = Patience(a);
                if (a.QueueSeconds >= patience) { LeaveQueueAngry(a); continue; }
                float ratio = a.QueueSeconds / patience;
                if (!a.SaidImpatient && ratio >= ImpatientAt)
                {
                    a.SaidImpatient = a.SaidWaiting = true;
                    Say(a, CustomerMoment.Impatient);
                }
                else if (!a.SaidWaiting && ratio >= WaitingAt)
                {
                    a.SaidWaiting = true;
                    if (a != checkoutAgent && Chance(lineSet != null ? lineSet.waitingChance : 0.5f)) Say(a, CustomerMoment.Waiting);
                }
            }
        }

        float WaitingAt => lineSet != null ? lineSet.waitingAt : 0.35f;
        float ImpatientAt => lineSet != null ? lineSet.impatientAt : 0.75f;

        // 계산 중인 손님은 물건이 2개째부터 한 개당 더 기다려 줌 (계산시스템_확장_설계 K6)
        float Patience(CustomerAgent agent)
        {
            if (context == null) return 30f;
            float seconds = context.Campaign.customerPatienceSeconds;
            if (agent == checkoutAgent && counter != null)
                seconds += context.Campaign.patiencePerExtraItemSeconds * Mathf.Max(0, counter.ItemCount - 1);
            return seconds;
        }

        // ------------------------------------------------------------------ 손님 흐름

        void Spawn()
        {
            int index = picker.Next();
            if (index < 0) return;
            CustomerRoster.Entry entry = roster.entries[index];
            if (entry.prefab == null) return;

            GameObject go = Instantiate(entry.prefab, outside.position, outside.rotation, transform);
            go.name = $"Customer_{entry.prefab.name}";

            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false; // 이동은 CustomerAgent가 함 (제자리 걷기 클립)
                animator.runtimeAnimatorController = roster.ControllerFor(entry.gender);
            }

            // 키 맞추기 (플레이 피드백: 손님이 너무 큼). 크기를 줄이면 보폭도 줄어드므로 걷는 속도도 같은 비율로.
            float scale = FitHeight(go, animator, roster.HeadHeightFor(entry.gender));

            var agent = go.AddComponent<CustomerAgent>();
            agent.Init(this, animator, roster.walkSpeed * scale, roster.turnSpeedDegrees);
            agent.Bubble = SpeechBubble.Create(go.transform, HumanoidHeight.Head(animator), bubbleFont);
            agent.Bubble.IsPaused = () => IsPaused;
            agents.Add(agent);

            agent.State = CustomerState.Enter;
            agent.Gesture(CustomerAnim.Wave);
            if (Chance(lineSet != null ? lineSet.enterChance : 0.5f)) Say(agent, CustomerMoment.Enter);

            Transform spot = PickBrowseSpot(agent);
            if (spot != null) agent.WalkPath(Routes.EnterToBrowse(spot.position), spot.rotation, () => StartBrowse(agent));
            else
            {
                // 구경 자리가 다 참: 남의 자리에 겹쳐 서지 않고 바로 줄 끝으로
                agent.BrowsePosition = door.position;
                JoinQueue(agent, fromBrowse: false);
            }
        }

        /// <summary>머리 뼈 높이가 range 안의 무작위 값이 되도록 균일 축소/확대. 적용한 배율을 돌려줍니다.</summary>
        float FitHeight(GameObject go, Animator animator, Vector2 range) => HumanoidHeight.Fit(go, animator, RandomIn(range));

        /// <summary>비어 있는 구경 자리 하나 (없으면 null)</summary>
        Transform PickBrowseSpot(CustomerAgent agent)
        {
            var free = new List<int>();
            for (int i = 0; i < browseSpots.Length; i++)
            {
                bool taken = false;
                foreach (CustomerAgent a in agents) if (a != agent && a.BrowseSlot == i) taken = true;
                if (!taken) free.Add(i);
            }
            if (free.Count == 0) return null;
            int slot = free[context.Random.Range(0, free.Count)];
            agent.BrowseSlot = slot;
            agent.BrowsePosition = browseSpots[slot].position;
            return browseSpots[slot];
        }

        void StartBrowse(CustomerAgent agent)
        {
            agent.State = CustomerState.Browse;
            agent.Gesture(CustomerAnim.Question);
            if (Chance(lineSet != null ? lineSet.browseChance : 0.6f)) Say(agent, CustomerMoment.Browse);
            agent.Wait(RandomIn(roster.browseSeconds), () => JoinQueue(agent, fromBrowse: true));
        }

        // range.x ~ range.y 사이 균등 (x > y면 x)
        float RandomIn(Vector2 range) => range.x + (float)context.Random.NextDouble() * Mathf.Max(0f, range.y - range.x);

        bool Chance(float p) => context != null && context.Random.NextDouble() < p;

        void JoinQueue(CustomerAgent agent, bool fromBrowse)
        {
            agent.BrowseSlot = -1;
            agent.State = CustomerState.Queue;
            agent.QueueSeconds = 0f;
            agent.InLine = false;
            queue.Add(agent);

            int index = queue.Count - 1;
            SlotPose(index, out Vector3 tail, out Quaternion rot);
            Vector3[] path = fromBrowse ? Routes.BrowseToQueue(agent.transform.position, tail) : Routes.EnterToQueue(tail);
            if (entryTurn == null || aisleLow == null) path = new[] { tail }; // 예전 씬(통로 지점 없음): 곧장
            agent.WalkPath(path, rot, () => ArriveInLine(agent));
        }

        // 줄 칸에 도착: 그새 줄이 당겨졌으면 줄을 따라 지금 칸으로, 맨 앞이면 계산 시작
        void ArriveInLine(CustomerAgent agent)
        {
            if (agent.State != CustomerState.Queue) return;
            agent.InLine = true;
            int index = queue.IndexOf(agent);
            if (index < 0) return;
            SlotPose(index, out Vector3 pos, out Quaternion rot);
            Vector3 flat = agent.transform.position - pos;
            flat.y = 0f;
            if (flat.magnitude > 0.05f) { agent.WalkTo(pos, rot, () => ArriveInLine(agent)); return; }
            if (index == 0) PrepareCheckout(agent);
        }

        /// <summary>
        /// 줄 맨 앞에 도착한 손님이 장바구니를 카운터에 올립니다. 무엇을 몇 개 살지 · 바코드 · 결제는 CheckoutCounter가 맡고,
        /// 결제가 끝나면 OnPaid로 돌아옵니다.
        /// </summary>
        void PrepareCheckout(CustomerAgent agent)
        {
            if (checkoutAgent == agent || queue.Count == 0 || queue[0] != agent || agent.State != CustomerState.Queue) return;
            agent.Gesture(CustomerAnim.Talk);
            Say(agent, CustomerMoment.Checkout);
            checkoutAgent = agent;
            if (counter != null) counter.Begin((bought, count, net) => OnPaid(agent, bought, count, net));
            else Debug.LogWarning("[CustomerDirector] CheckoutCounter가 없어 계산할 수 없습니다 (Build Shop Scene 다시 실행).", this);
        }

        // 줄 칸 위치 · 방향. 준비된 칸보다 줄이 길면 마지막 두 칸 간격대로 이어서 섭니다.
        void SlotPose(int index, out Vector3 pos, out Quaternion rot)
        {
            var points = new Vector3[queueSlots.Length];
            for (int i = 0; i < points.Length; i++) points[i] = queueSlots[i].position;
            Transform last = queueSlots[queueSlots.Length - 1];
            pos = CustomerRoutes.SlotPosition(points, last.forward, index);
            rot = queueSlots[Mathf.Min(index, queueSlots.Length - 1)].rotation;
        }

        // 떠나는 손님이 칸을 비운 뒤: 줄에 이미 선 손님은 줄을 따라 한 칸씩 앞으로. 걸어오는 중인 손님은 도착한 뒤 ArriveInLine이 맞춤
        void ShiftQueue()
        {
            for (int i = 0; i < queue.Count; i++)
            {
                CustomerAgent a = queue[i];
                if (a.State != CustomerState.Queue || !a.InLine) continue;
                SlotPose(i, out Vector3 pos, out Quaternion rot);
                a.WalkTo(pos, rot, () => ArriveInLine(a));
            }
        }

        void OnPaid(CustomerAgent agent, CardData bought, int itemCount, int net)
        {
            checkoutAgent = null;
            agent.State = CustomerState.Leave; // 인사하는 동안은 맨 앞 칸을 계속 차지 (다음 손님이 겹쳐 들어오지 않게)

            context.Events.RaiseCustomerServed(bought);
            if (bought != null)
            {
                context.Events.RaiseNotice($"손님이 판매 등록한 「{bought.displayName}」을(를) 샀어요! (정산 때 +{net})");
                agent.Gesture(CustomerAnim.Cheer);
                Say(agent, CustomerMoment.PaidCard, bought.displayName);
            }
            else
            {
                context.Events.RaiseNotice(itemCount > 1 ? $"손님이 가게 재고 {itemCount}개를 사 갔어요." : "손님이 가게 재고를 사 갔어요.");
                agent.Gesture(CustomerAnim.Nod);
                Say(agent, CustomerMoment.Paid);
            }

            agent.Wait(reactionSeconds, () => LeaveLine(agent, wave: true));
        }

        void LeaveQueueAngry(CustomerAgent agent)
        {
            if (agent == checkoutAgent) CancelCheckout(); // 물건을 두고 가지 않음, 판매도 없음
            context.Session.Ledger.customersLost++;
            agent.State = CustomerState.Leave; // 화내는 동안은 자기 칸을 계속 차지
            agent.Gesture(CustomerAnim.Angry);
            Say(agent, CustomerMoment.Angry);
            context.Events.RaiseNotice("손님이 기다리다 그냥 나갔어요.");
            agent.Wait(angrySeconds, () => LeaveLine(agent, wave: false));
        }

        // 줄에서 빠져 걸어 나가기 시작: 이때 칸이 비므로 줄을 당김
        void LeaveLine(CustomerAgent agent, bool wave)
        {
            int index = queue.IndexOf(agent);
            queue.Remove(agent);
            ShiftQueue();
            if (wave) agent.Gesture(CustomerAnim.Wave);

            CustomerRoutes routes = Routes;
            Vector3[] path;
            if (entryTurn == null || aisleLow == null || counterExit == null) path = doorOutside != null
                ? new[] { door.position, doorOutside.position, outside.position }
                : new[] { door.position, outside.position }; // 예전 씬
            else if (index == 0 && agent.InLine) path = routes.LeaveFromCounter();
            else path = routes.LeaveFromQueue(agent.transform.position, queueSlots[0].position);

            agent.WalkPath(path, null, () =>
            {
                context.Events.RaiseCustomerLeft(agent.BrowsePosition);
                Remove(agent);
            });
        }

        void Say(CustomerAgent agent, CustomerMoment moment, string cardName = null)
        {
            if (agent == null || agent.Bubble == null) return;
            lines ??= new CustomerLines(lineSet != null ? lineSet.lines : null, context != null ? context.Random : null);
            agent.Bubble.Say(lines.Pick(moment, cardName), lineSet != null ? lineSet.bubbleSeconds : 2.8f);
        }

        void Remove(CustomerAgent agent)
        {
            agents.Remove(agent);
            queue.Remove(agent);
            if (agent != null) Destroy(agent.gameObject);
        }

        // ------------------------------------------------------------------ ICustomerFloor

        public void StopSpawning() => spawning = false;

        void CancelCheckout()
        {
            checkoutAgent = null;
            if (counter != null) counter.Cancel();
        }

        public void DismissAll()
        {
            CancelCheckout();
            foreach (CustomerAgent a in agents)
                if (a != null) Destroy(a.gameObject);
            agents.Clear();
            queue.Clear();
        }
    }
}
