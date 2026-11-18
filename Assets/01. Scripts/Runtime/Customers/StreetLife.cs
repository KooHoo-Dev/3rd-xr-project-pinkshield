using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 가게 앞 거리의 생동감: 지나가는 차와 행인 (2026-11-18 기기 테스트 피드백 M3).
    /// - 차: 도로 차선을 따라 x 방향으로 직선 이동만 (물리 · 경로 탐색 없음). 거리 양 끝 건물 안에서 나타나고 사라짐.
    /// - 행인: 손님 외형(CustomerRoster)과 걷기 애니메이션을 재사용해 인도를 따라 걷기만 함 (가게에 들어오지 않음).
    /// Quest 성능 때문에 동시에 차 3대 · 행인 4명까지, 그림자 끔, 애니메이션은 화면 밖에서 갱신 안 함.
    /// 일시정지 중에는 멈추고, 집 페이즈에는 ZoneVisibility가 이 오브젝트째 꺼서 숨깁니다.
    /// </summary>
    public sealed class StreetLife : MonoBehaviour
    {
        [System.Serializable]
        public struct Lane
        {
            [Tooltip("차선 · 인도 줄의 z")]
            public float z;
            [Tooltip("+1 = +X 방향으로 감, -1 = -X 방향")]
            public int direction;
        }

        [SerializeField] GameFlow flow;

        [Header("차")]
        [SerializeField] GameObject[] carPrefabs;
        [SerializeField] Lane[] carLanes = { new Lane { z = 12.9f, direction = 1 }, new Lane { z = 15.5f, direction = -1 } };
        [Tooltip("도로 윗면 높이")]
        [SerializeField] float roadY = -0.15f;
        [SerializeField] Vector2 carSpeed = new Vector2(7f, 10f);
        [SerializeField] Vector2 carGapSeconds = new Vector2(4f, 10f);
        [SerializeField] int maxCars = 3;

        [Header("행인")]
        [SerializeField] CustomerRoster roster;
        [SerializeField] Lane[] walkLanes =
        {
            new Lane { z = 20.6f, direction = 1 }, new Lane { z = 22.0f, direction = -1 }, new Lane { z = 7.2f, direction = -1 },
        };
        [SerializeField] Vector2 walkSpeed = new Vector2(1.0f, 1.4f);
        [SerializeField] Vector2 walkerGapSeconds = new Vector2(5f, 12f);
        [SerializeField] int maxWalkers = 4;

        [Header("거리 끝 (양 끝 건물 안에서 나타나고 사라짐)")]
        [SerializeField] float endX = 52f;

        struct Mover
        {
            public Transform t;
            public Animator animator;
            public float speed;
            public int direction;
            public Transform[] wheels;      // 차 바퀴 (PolygonCity 차는 바퀴가 따로 있음, 이름에 _Wheel_)
            public Vector3[] wheelCenters;  // 바퀴 가운데 (차 기준 로컬)
            public float wheelRadius;
        }

        readonly List<Mover> cars = new List<Mover>();
        readonly List<Mover> walkers = new List<Mover>();
        readonly IRandomSource random = new SystemRandomSource();
        float nextCar, nextWalker;

        public void Configure(GameFlow gameFlow, GameObject[] cars, CustomerRoster people)
        {
            flow = gameFlow;
            carPrefabs = cars;
            roster = people;
        }

        void Start()
        {
            // 처음부터 거리가 비어 보이지 않게 몇 개를 중간에 미리 둠
            for (int i = 0; i < 2; i++) SpawnCar(Range(-endX * 0.8f, endX * 0.8f));
            for (int i = 0; i < 2; i++) SpawnWalker(Range(-endX * 0.8f, endX * 0.8f));
            nextCar = Range(carGapSeconds);
            nextWalker = Range(walkerGapSeconds);
        }

        void Update()
        {
            if (flow != null && flow.IsPaused)
            {
                SetAnimatorSpeed(0f);
                return;
            }
            SetAnimatorSpeed(1f);
            float dt = Time.deltaTime;

            nextCar -= dt;
            if (nextCar <= 0f) { SpawnCar(null); nextCar = Range(carGapSeconds); }
            nextWalker -= dt;
            if (nextWalker <= 0f) { SpawnWalker(null); nextWalker = Range(walkerGapSeconds); }

            Move(cars, dt);
            Move(walkers, dt);
        }

        void Move(List<Mover> list, float dt)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Mover m = list[i];
                if (m.t == null) { list.RemoveAt(i); continue; }
                m.t.position += Vector3.right * (m.direction * m.speed * dt);
                SpinWheels(m, m.speed * dt);
                if (m.t.position.x * m.direction > endX)
                {
                    Destroy(m.t.gameObject);
                    list.RemoveAt(i);
                }
            }
        }

        void SetAnimatorSpeed(float s)
        {
            foreach (Mover m in walkers) if (m.animator != null) m.animator.speed = s;
        }

        // startX가 null이면 거리 끝에서 나타남
        void SpawnCar(float? startX)
        {
            if (carPrefabs == null || carPrefabs.Length == 0 || carLanes.Length == 0 || cars.Count >= maxCars) return;
            GameObject prefab = carPrefabs[random.Range(0, carPrefabs.Length)];
            if (prefab == null) return;
            Lane lane = carLanes[random.Range(0, carLanes.Length)];
            if (LaneBusy(cars, lane, startX, 9f)) return;

            float x = startX ?? -lane.direction * endX;
            GameObject go = Instantiate(prefab, new Vector3(x, roadY, lane.z), Quaternion.Euler(0f, 90f * lane.direction, 0f), transform);
            go.name = "TrafficCar";
            Strip(go);
            // 모델 원점이 바닥이 아닐 수 있어 바퀴 바닥을 도로 윗면에 맞춤
            Bounds b = Bounds(go);
            go.transform.position += Vector3.up * (roadY - b.min.y);
            var car = new Mover { t = go.transform, speed = Range(carSpeed), direction = lane.direction };
            FindWheels(ref car);
            cars.Add(car);
        }

        void SpawnWalker(float? startX)
        {
            if (roster == null || roster.entries.Count == 0 || walkLanes.Length == 0 || walkers.Count >= maxWalkers) return;
            CustomerRoster.Entry entry = roster.entries[random.Range(0, roster.entries.Count)];
            if (entry.prefab == null) return;
            Lane lane = walkLanes[random.Range(0, walkLanes.Length)];
            if (LaneBusy(walkers, lane, startX, 2.5f)) return;

            float x = startX ?? -lane.direction * endX;
            GameObject go = Instantiate(entry.prefab, new Vector3(x, 0f, lane.z), Quaternion.Euler(0f, 90f * lane.direction, 0f), transform);
            go.name = "Passerby";
            var animator = go.GetComponentInChildren<Animator>();
            float scale = 1f;
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = roster.ControllerFor(entry.gender);
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                Vector2 h = roster.HeadHeightFor(entry.gender);
                scale = HumanoidHeight.Fit(go, animator, Range(h));
            }
            float speed = Range(walkSpeed) * scale;
            if (animator != null && animator.runtimeAnimatorController != null) animator.SetFloat(CustomerAnim.Speed, speed);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            walkers.Add(new Mover { t = go.transform, animator = animator, speed = speed, direction = lane.direction });
        }

        // 같은 줄의 시작 지점 근처에 이미 누가 있으면 겹치지 않게 이번에는 건너뜀
        bool LaneBusy(List<Mover> list, Lane lane, float? startX, float gap)
        {
            float x = startX ?? -lane.direction * endX;
            foreach (Mover m in list)
                if (m.t != null && Mathf.Abs(m.t.position.z - lane.z) < 0.1f && Mathf.Abs(m.t.position.x - x) < gap) return true;
            return false;
        }

        // 4차 기기 피드백: 바퀴가 안 굴러 차가 미끄러지듯 보였음. 바퀴 오브젝트를 찾아 달린 거리만큼 굴림
        static void FindWheels(ref Mover car)
        {
            var wheels = new List<Transform>();
            foreach (Transform c in car.t.GetComponentsInChildren<Transform>())
                if (c.name.Contains("_Wheel_") && !c.name.Contains("Steering")) wheels.Add(c);
            if (wheels.Count == 0) return;
            car.wheels = wheels.ToArray();
            car.wheelCenters = new Vector3[wheels.Count];
            float radius = 0f;
            for (int i = 0; i < wheels.Count; i++)
            {
                Renderer r = wheels[i].GetComponent<Renderer>();
                Vector3 center = r != null ? r.bounds.center : wheels[i].position;
                car.wheelCenters[i] = car.t.InverseTransformPoint(center);
                if (r != null) radius = Mathf.Max(radius, r.bounds.extents.y);
            }
            car.wheelRadius = radius > 0.05f ? radius : 0.35f;
        }

        // 앞으로 distance(m) 굴러감: 차 오른쪽 축 기준, 바퀴 윗부분이 앞으로 가는 방향
        static void SpinWheels(Mover car, float distance)
        {
            if (car.wheels == null) return;
            float degrees = distance / car.wheelRadius * Mathf.Rad2Deg;
            Vector3 axle = car.t.right;
            for (int i = 0; i < car.wheels.Length; i++)
                if (car.wheels[i] != null) car.wheels[i].RotateAround(car.t.TransformPoint(car.wheelCenters[i]), axle, degrees);
        }

        static void Strip(GameObject go)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.isStatic = false;
        }

        static Bounds Bounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        float Range(Vector2 r) => Range(r.x, r.y);
        float Range(float a, float b) => a + (float)random.NextDouble() * Mathf.Max(0f, b - a);
    }
}
