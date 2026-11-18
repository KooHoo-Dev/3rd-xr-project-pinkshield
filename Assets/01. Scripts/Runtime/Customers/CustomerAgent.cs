using System;
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
    }

    public enum CustomerState
    {
        Enter,    // 문 → 진열대
        Browse,   // 진열대 앞에서 구경
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

        CustomerDirector director;
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
        public bool IsMoving => path != null;
        /// <summary>진열대에서 구경한 자리 (퇴장 시 청소 구역 위치로 알림)</summary>
        public Vector3 BrowsePosition { get; set; }
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

        public void Init(CustomerDirector owner, Animator anim, float speed, float turnDegreesPerSecond)
        {
            director = owner;
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

        public void Gesture(int trigger)
        {
            if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(trigger);
        }

        void Update()
        {
            bool paused = director != null && director.IsPaused;
            if (animator != null) animator.speed = paused ? 0f : 1f;
            if (paused) return;

            float dt = Time.deltaTime;
            if (State == CustomerState.Queue) QueueSeconds += dt;
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

            if (animator != null) animator.SetFloat(CustomerAnim.Speed, path != null ? walkSpeed : 0f);
        }

        void Move(float dt)
        {
            Vector3 target = path[pathIndex];
            Vector3 to = target - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            if (dist > ArriveDistance)
            {
                transform.position = Vector3.MoveTowards(transform.position, new Vector3(target.x, transform.position.y, target.z), walkSpeed * dt);
                TurnTo(Quaternion.LookRotation(to / dist, Vector3.up), dt);
                return;
            }

            if (++pathIndex < path.Length) return;

            path = null;
            Action arrived = onArrive;
            onArrive = null;
            arrived?.Invoke();
        }

        void TurnTo(Quaternion goal, float dt)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, turnSpeed * dt);
            if (path == null && Quaternion.Angle(transform.rotation, goal) < 0.5f) faceOnArrive = null;
        }
    }
}
