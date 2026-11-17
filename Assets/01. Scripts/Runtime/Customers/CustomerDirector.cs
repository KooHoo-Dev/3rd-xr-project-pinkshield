using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 영업 중 손님 흐름 (설계서 7-4, Q1 · Q2 · Q10 / 타이틀연출_손님_설계 3장).
    /// 등장 일정 → 입장 → 진열대 구경 → 계산대 줄 → 계산(CheckoutCounter) 또는 인내 시간 초과 → 퇴장.
    /// 외형은 CustomerRoster에서 그룹 가중치로 뽑습니다. 줄 관리 · 이벤트는 여기, 장바구니 · 바코드 · 결제는 CheckoutCounter,
    /// 걷기 · 제스처는 CustomerAgent.
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
        [Tooltip("진열대 앞 구경 자리. forward = 진열대 쪽")]
        [SerializeField] Transform[] browseSpots;
        [Tooltip("계산대 줄. 0번이 맨 앞(결제 자리), forward = 플레이어 쪽")]
        [SerializeField] Transform[] queueSlots;

        [Header("계산")]
        [Tooltip("장바구니 · 바코드 · 포스기 · 결제를 맡는 계산대")]
        [SerializeField] CheckoutCounter counter;

        [Header("연출 시간(초)")]
        [SerializeField] float reactionSeconds = 1.6f;
        [SerializeField] float angrySeconds = 1.8f;

        GameContext context;
        CustomerPicker picker;
        readonly List<CustomerAgent> agents = new List<CustomerAgent>();
        readonly List<CustomerAgent> queue = new List<CustomerAgent>();

        // 지금 계산대에 물건을 올린 손님 (줄 맨 앞 한 명뿐)
        CustomerAgent checkoutAgent;

        float[] schedule = new float[0];
        int nextSpawn;
        float elapsed;
        bool spawning;

        public bool IsPaused => flow != null && flow.IsPaused;
        public bool HasCustomers => agents.Count > 0 || (spawning && nextSpawn < schedule.Length);
        public int ActiveCount => agents.Count;
        /// <summary>지금 매장 안팎에 있는 손님 (입구 종 등 연출이 읽기만 함)</summary>
        public IReadOnlyList<CustomerAgent> Agents => agents;
        /// <summary>출입문 지점 (입구 종이 문을 지나는지 판단할 때 사용)</summary>
        public Transform Door => door;

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

            CampaignConfig.DayEntry day = context.Campaign.GetDay(context.Session.Day);
            schedule = CustomerSchedule.Build(day, context.Campaign.spawnWindowRatio, context.Random);
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

            // 인내 시간 초과 (설계서 Q2): 감점 없이 그냥 떠남
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                CustomerAgent a = queue[i];
                if (a.QueueSeconds >= Patience(a)) LeaveQueueAngry(a);
            }
        }

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
            agents.Add(agent);

            agent.State = CustomerState.Enter;
            agent.Gesture(CustomerAnim.Wave);
            Transform spot = PickBrowseSpot(agent);
            agent.WalkPath(EnterPath(spot.position), spot.rotation, () => StartBrowse(agent));
        }

        /// <summary>머리 뼈 높이가 range 안의 무작위 값이 되도록 균일 축소/확대. 적용한 배율을 돌려줍니다.</summary>
        float FitHeight(GameObject go, Animator animator, Vector2 range)
        {
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            float current = head != null ? head.position.y - go.transform.position.y : 0f;
            if (current < 0.5f) return 1f; // 뼈를 못 찾음: 크기 그대로

            float target = range.x + (float)context.Random.NextDouble() * Mathf.Max(0f, range.y - range.x);
            float scale = target / current;
            go.transform.localScale *= scale;
            return scale;
        }

        Transform PickBrowseSpot(CustomerAgent agent)
        {
            var free = new List<int>();
            for (int i = 0; i < browseSpots.Length; i++)
            {
                bool taken = false;
                foreach (CustomerAgent a in agents) if (a != agent && a.BrowseSlot == i) taken = true;
                if (!taken) free.Add(i);
            }
            int slot = free.Count > 0 ? free[context.Random.Range(0, free.Count)] : context.Random.Range(0, browseSpots.Length);
            agent.BrowseSlot = slot;
            agent.BrowsePosition = browseSpots[slot].position;
            return browseSpots[slot];
        }

        void StartBrowse(CustomerAgent agent)
        {
            agent.State = CustomerState.Browse;
            agent.Gesture(CustomerAnim.Question);
            Vector2 range = roster.browseSeconds;
            float seconds = range.x + (float)context.Random.NextDouble() * Mathf.Max(0f, range.y - range.x);
            agent.Wait(seconds, () => JoinQueue(agent));
        }

        void JoinQueue(CustomerAgent agent)
        {
            agent.BrowseSlot = -1;
            agent.State = CustomerState.Queue;
            agent.QueueSeconds = 0f;
            queue.Add(agent);
            MoveToSlot(agent, queue.Count - 1);
        }

        void MoveToSlot(CustomerAgent agent, int index)
        {
            SlotPose(index, out Vector3 pos, out Quaternion rot);
            bool front = index == 0;
            agent.WalkTo(pos, rot, front ? () => PrepareCheckout(agent) : (System.Action)null);
        }

        /// <summary>
        /// 줄 맨 앞에 도착한 손님이 장바구니를 카운터에 올립니다. 무엇을 몇 개 살지 · 바코드 · 결제는 CheckoutCounter가 맡고,
        /// 결제가 끝나면 OnPaid로 돌아옵니다.
        /// </summary>
        void PrepareCheckout(CustomerAgent agent)
        {
            if (checkoutAgent == agent || queue.Count == 0 || queue[0] != agent) return;
            agent.Gesture(CustomerAnim.Talk);
            checkoutAgent = agent;
            if (counter != null) counter.Begin((bought, count) => OnPaid(agent, bought, count));
            else Debug.LogWarning("[CustomerDirector] CheckoutCounter가 없어 계산할 수 없습니다 (Build Shop Scene 다시 실행).", this);
        }

        // 줄이 준비된 칸보다 길면 마지막 칸 뒤로 같은 간격만큼 이어서 섭니다.
        void SlotPose(int index, out Vector3 pos, out Quaternion rot)
        {
            int last = queueSlots.Length - 1;
            if (index <= last)
            {
                pos = queueSlots[index].position;
                rot = queueSlots[index].rotation;
                return;
            }
            Vector3 step = last > 0 ? queueSlots[last].position - queueSlots[last - 1].position : -queueSlots[last].forward * 0.6f;
            pos = queueSlots[last].position + step * (index - last);
            rot = queueSlots[last].rotation;
        }

        void ShiftQueue()
        {
            for (int i = 0; i < queue.Count; i++) MoveToSlot(queue[i], i);
        }

        void OnPaid(CustomerAgent agent, CardData bought, int itemCount)
        {
            checkoutAgent = null;
            queue.Remove(agent);
            ShiftQueue();

            context.Events.RaiseCustomerServed(bought);
            if (bought != null)
            {
                int net = context.Session.Ledger.sales[context.Session.Ledger.sales.Count - 1].net;
                context.Events.RaiseNotice($"손님이 판매 등록한 「{bought.displayName}」을(를) 샀어요! (정산 때 +{net})");
                agent.Gesture(CustomerAnim.Cheer);
            }
            else
            {
                context.Events.RaiseNotice(itemCount > 1 ? $"손님이 가게 재고 {itemCount}개를 사 갔어요." : "손님이 가게 재고를 사 갔어요.");
                agent.Gesture(CustomerAnim.Nod);
            }

            agent.State = CustomerState.Leave;
            agent.Wait(reactionSeconds, () => Leave(agent, wave: true));
        }

        void LeaveQueueAngry(CustomerAgent agent)
        {
            if (agent == checkoutAgent) CancelCheckout(); // 물건을 두고 가지 않음, 판매도 없음
            queue.Remove(agent);
            ShiftQueue();
            agent.State = CustomerState.Leave;
            agent.Gesture(CustomerAnim.Angry);
            context.Events.RaiseNotice("손님이 기다리다 그냥 나갔어요.");
            agent.Wait(angrySeconds, () => Leave(agent, wave: false));
        }

        void Leave(CustomerAgent agent, bool wave)
        {
            if (wave) agent.Gesture(CustomerAnim.Wave);
            agent.WalkPath(ExitPath(), null, () =>
            {
                context.Events.RaiseCustomerLeft(agent.BrowsePosition);
                Remove(agent);
            });
        }

        // 밖 → (문 바깥) → 문 → 목적지. 문 바깥 지점이 없으면 예전처럼 밖 → 문.
        Vector3[] EnterPath(Vector3 target) => doorOutside != null
            ? new[] { doorOutside.position, door.position, target }
            : new[] { door.position, target };

        Vector3[] ExitPath() => doorOutside != null
            ? new[] { door.position, doorOutside.position, outside.position }
            : new[] { door.position, outside.position };

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
