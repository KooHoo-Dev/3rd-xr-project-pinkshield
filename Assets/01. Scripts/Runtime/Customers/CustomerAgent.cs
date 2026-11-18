using System;
using System.Collections.Generic;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>Customer.controller의 파라미터 이름 (CustomerAnimatorBuilder와 같게).</summary>
    public static class CustomerAnim
    {
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int Wave = Animator.StringToHash("Wave");
        public static readonly int Question = Animator.StringToHash("Question");
        public static readonly int Nod = Animator.StringToHash("Nod");
        public static readonly int Shake = Animator.StringToHash("Shake");
        public static readonly int Cheer = Animator.StringToHash("Cheer");
        public static readonly int Angry = Animator.StringToHash("Angry");
        public static readonly int Talk = Animator.StringToHash("Talk");
        /// <summary>전신 휘청 (Reaction 레이어, 도둑이 빗자루에 맞음). 예전 Animator에는 없어 무시됨 → Build Customer Animator</summary>
        public static readonly int Hit = Animator.StringToHash("Hit");
    }

    public enum CustomerState
    {
        Enter,      // 문 → 진열대
        Browse,     // 진열대 뒤 구매 자리에서 구경
        BrowseWait, // 구매 자리가 비기를 기다리는 줄 (창을 따라 섬, 11-19 손님 동선)
        Queue,    // 계산대 줄 (맨 앞이면 결제 대기)
        Leave,    // 문 → 밖
    }

    /// <summary>
    /// 손님 한 명의 이동 · 애니메이션 (설계서 7-4). 무엇을 살지와 줄 관리는 CustomerDirector가 정하고,
    /// 이 컴포넌트는 "어디로 걸어가 어느 쪽을 보고 설지"와 제스처만 맡습니다.
    /// 매장이 단순해 NavMesh 대신 웨이포인트를 직선으로 걷습니다.
    /// 일시정지(GameFlow.IsPaused) 중에는 이동 · 타이머 · 애니메이션이 모두 멈춥니다.
    /// </summary>
    public sealed class CustomerAgent : MonoBehaviour
    {
        const float ArriveDistance = 0.03f;

        // 매장에 있는 손님들 (서로 피하기용)
        static readonly List<CustomerAgent> Active = new List<CustomerAgent>();
        readonly List<SteeringNeighbor> neighborBuffer = new List<SteeringNeighbor>();

        [Tooltip("손님 한 명이 차지하는 반지름 (m). 걸을 때 이 거리 안으로는 서로 비켜 감")]
        [SerializeField] float avoidRadius = 0.35f;

        Vector3 velocity;
        Func<bool> isPaused;
        Animator animator;
        float walkSpeed;
        float turnSpeed;

        Vector3[] path;
        int pathIndex;
        Quaternion? faceOnArrive;
        Action onArrive;

        float waitLeft;
        Action onWaitDone;

        public CustomerState State { get; set; }
        /// <summary>걷는 속도 (m/s). 도둑이 달아날 때 올림</summary>
        public float WalkSpeed { get => walkSpeed; set => walkSpeed = Mathf.Max(0.1f, value); }
        /// <summary>지금 실제로 움직이는 속도 (m/s)</summary>
        public Vector3 Velocity => velocity;
        public int BrowseSlot { get; set; } = -1;
        /// <summary>줄에 선 뒤 흐른 시간(초). 인내 시간 판정용</summary>
        public float QueueSeconds { get; set; }
        /// <summary>줄 칸에 한 번이라도 도착했는가 (아직 줄로 걸어오는 중이면 false: 줄이 당겨져도 길을 바꾸지 않고 도착 뒤 줄을 따라 이동)</summary>
        public bool InLine { get; set; }
        /// <summary>줄에서 '기다리는 중' · '재촉' 대사를 이미 했는가</summary>
        public bool SaidWaiting { get; set; }
        public bool SaidImpatient { get; set; }
        /// <summary>머리 위 말풍선 (CustomerDirector가 만듦, 없을 수 있음)</summary>
        public SpeechBubble Bubble { get; set; }

        /// <param name="paused">일시정지 중인가 (이동 · 타이머 · 애니메이션을 멈춤)</param>
        public void Init(Func<bool> paused, Animator anim, float speed, float turnDegreesPerSecond)
        {
            isPaused = paused;
            animator = anim;
            walkSpeed = speed;
            turnSpeed = turnDegreesPerSecond;
        }

        /// <summary>점들을 차례로 걸어가고, 도착하면 face 방향을 본 뒤 arrived를 부릅니다.</summary>
        public void WalkPath(Vector3[] points, Quaternion? face, Action arrived)
        {
            path = points;
            pathIndex = 0;
            faceOnArrive = face;
            onArrive = arrived;
        }

        public void WalkTo(Vector3 point, Quaternion? face, Action arrived) => WalkPath(new[] { point }, face, arrived);

        /// <summary>제자리에서 seconds초 기다린 뒤 done (일시정지 중에는 시간이 흐르지 않음).</summary>
        public void Wait(float seconds, Action done)
        {
            waitLeft = seconds;
            onWaitDone = done;
        }

        void OnEnable() => Active.Add(this);

        void OnDisable()
        {
            Active.Remove(this);
            velocity = Vector3.zero;
        }

        public void Gesture(int trigger)
        {
            if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(trigger);
        }

        void Update()
        {
            bool paused = isPaused != null && isPaused();
            if (animator != null) animator.speed = paused ? 0f : 1f;
            if (paused) return;

            float dt = Time.deltaTime;
            if (State == CustomerState.Queue && InLine) QueueSeconds += dt; // 줄 끝에 도착한 뒤부터 (걸어오는 동안은 안 셈)
            if (path != null) Move(dt);
            else if (faceOnArrive.HasValue) TurnTo(faceOnArrive.Value, dt);

            if (onWaitDone != null)
            {
                waitLeft -= dt;
                if (waitLeft <= 0f)
                {
                    Action done = onWaitDone;
                    onWaitDone = null;
                    done();
                }
            }

            // 실제로 걸은 속도 (다른 손님 뒤에서 멈칫하면 걷는 동작도 느려짐)
            if (animator != null) animator.SetFloat(CustomerAnim.Speed, path != null ? velocity.magnitude : 0f);
        }

        void Move(float dt)
        {
            Vector3 target = path[pathIndex];
            Vector3 to = target - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            if (dist > ArriveDistance)
            {
                Vector3 before = transform.position;
                Vector3 flatTarget = new Vector3(target.x, before.y, target.z);
                if (dist <= walkSpeed * dt)
                {
                    transform.position = flatTarget; // 이번 프레임에 닿음
                }
                else
                {
                    // 다른 손님을 피해 걸음 (3차 기기 피드백: 서로 겹쳐 지나감)
                    Vector3 v = CustomerSteering.Velocity(before, flatTarget, walkSpeed, Neighbors(), avoidRadius);
                    transform.position = before + v * dt;
                }
                Vector3 moved = transform.position - before;
                velocity = dt > 0f ? moved / dt : Vector3.zero;
                Vector3 face = moved.sqrMagnitude > 1e-8f ? moved : to;
                face.y = 0f;
                if (face.sqrMagnitude > 1e-8f) TurnTo(Quaternion.LookRotation(face.normalized, Vector3.up), dt);
                return;
            }

            if (++pathIndex < path.Length) return;

            path = null;
            velocity = Vector3.zero;
            Action arrived = onArrive;
            onArrive = null;
            arrived?.Invoke();
        }

        // 지금 매장에 있는 다른 손님들 (몇 명뿐이라 매 프레임 모아도 가벼움)
        List<SteeringNeighbor> Neighbors()
        {
            neighborBuffer.Clear();
            foreach (CustomerAgent a in Active)
                if (a != this) neighborBuffer.Add(new SteeringNeighbor(a.transform.position, a.velocity));
            return neighborBuffer;
        }

        void TurnTo(Quaternion goal, float dt)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, turnSpeed * dt);
            if (path == null && Quaternion.Angle(transform.rotation, goal) < 0.5f) faceOnArrive = null;
        }
    }
}
