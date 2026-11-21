using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Customers;
using CardShop.Logic;
using CardShop.Presentation;
using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 영업 중 청소 구역 관리 (기획서 5-1 · 설계서 7-4, 창문 얼룩은 DEC-014로 제외: 바닥 쓰레기 · 흐트러진 팩 · 바닥 얼룩(DEC-030) 3종). 손님이 나갈 때
    /// 확률로(CleanupRules.ShouldSpawn, DEC-030) 그날 켜진 종류(CampaignConfig.cleanupKinds) 중 가중치로 하나를 만들고, 치우면 장부에 보너스 건수를 적습니다. 영업이 끝나면 BusinessPhase가 EndDay로 남은 구역을 걷어 내고
    /// 치우지 못한 수를 감점으로 적습니다. 청소 종류를 줄이려면 CampaignConfig의 cleanupKinds만 바꾸면 됩니다.
    /// </summary>
    public sealed class CleanupDirector : MonoBehaviour, ICleanupFloor
    {
        [SerializeField] GameFlow flow;

        [Header("바닥 쓰레기")]
        [SerializeField] TrashBin bin;
        [Tooltip("쓰레기 모양 원본 (꺼 둠). 하나를 골라 복제")]
        [SerializeField] TrashSpot[] trashTemplates;
        [Tooltip("쓰레기 크기 배율 (11-20: 여전히 작다는 피드백으로 1.5)")]
        [SerializeField] float trashScale = 1.5f;

        [Header("흐트러진 팩")]
        [SerializeField] PackRack rack;
        [Tooltip("손님 한 명이 떨어뜨리는 팩 수")]
        [SerializeField] int packsPerMess = 2;

        [Header("떨어지는 자리 (3차 기기 피드백: 한곳에 몰리고, 안 보이는 곳에 생겼음)")]
        [Tooltip("시야 밖에 생겨도 되는 확률 (둘러보게 하는 예외)")]
        [SerializeField, Range(0f, 1f)] float outOfViewChance = 0.15f;
        [Tooltip("청소 구역끼리 최소 거리 (m)")]
        [SerializeField] float minSeparation = 0.9f;
        [Tooltip("플레이어 시야 부채꼴 반각 (도)")]
        [SerializeField] float viewHalfAngle = 35f;

        [Header("흐트러진 팩 자리 (F3: 팩 꽂이 주변 + 계산대에서 보이는 곳)")]
        [Tooltip("진열대 앞면에서 바닥 띠까지 (가까운 쪽, 먼 쪽) 거리 (m). 가까운 쪽은 팩이 흩어지는 반경(0.4)보다 넓게 — 진열대 밑으로 안 들어가게")]
        [SerializeField] Vector2 messyBand = new Vector2(0.45f, 1.0f);

        // 계산대에 선 플레이어 눈 (XR Origin 자리 = 원점, 눈높이 1.6m)
        static readonly Vector3 CounterEye = new Vector3(0f, 1.6f, 0f);
        const int VisibleTries = 8;

        [Header("바닥 얼룩 (F5)")]
        [Tooltip("얼룩 지름 (최소, 최대) m")]
        [SerializeField] Vector2 stainDiameter = new Vector2(0.45f, 0.65f);

        GameContext context;
        CustomerDirector customers; // 얼룩 밟은 손님 확인용 (F6 ①)
        static readonly string[] SpillLines = { "앗, 쏟았다…", "어이쿠, 미안해요!", "헉, 음료가…" };
        Broom mop; // 대걸레 (MopStation이 빗자루를 바꾼 것). 없으면 얼룩을 만들지 않음
        CleanupPlacement placement, messyPlacement;
        Transform viewer;
        readonly List<CleanupSpot> active = new List<CleanupSpot>();
        readonly List<Vector3> activeSpots = new List<Vector3>();
        int spawnedToday, quietStreak; // 확률 생성용: 오늘 만든 수 · 연속으로 안 생긴 손님 수 (EndDay에서 0)

        public int PendingCount
        {
            get { int n = 0; foreach (CleanupSpot s in active) if (s != null && !s.IsDone) n++; return n; }
        }

        void OnEnable()
        {
            if (flow == null) return;
            context = flow.GetComponent<GameContext>();
            flow.Cleanup = this;
            if (bin != null) bin.IsPaused = () => flow.IsPaused;
            if (rack != null) rack.IsPaused = () => flow.IsPaused;
            if (context != null) context.Events.CustomerLeaving += OnCustomerLeaving;
        }

        void OnDisable()
        {
            if (flow != null && ReferenceEquals(flow.Cleanup, this)) flow.Cleanup = null;
            if (context != null) context.Events.CustomerLeaving -= OnCustomerLeaving;
        }

        void OnCustomerLeaving(Transform customer)
        {
            if (flow.CurrentPhase != GamePhaseId.Business) return;

            CampaignConfig.CleanupSpawnSettings rules = context.Campaign.cleanup;
            int day = context.Session.Day, pending = PendingCount;
            if (!CleanupRules.ShouldSpawn(rules, day, spawnedToday, pending, quietStreak, context.Random))
            {
                if (pending < rules.maxActive) quietStreak++; // 꽉 차서 못 만든 건 '안 생김'으로 세지 않음
                return;
            }
            CleanupKinds enabledToday = context.Campaign.GetDay(day).cleanupKinds;
            CleanupKinds kind = CleanupRules.Pick(enabledToday, Available(), rules, context.Random);
            if (kind == CleanupKinds.None) return;
            // 흐트러진 팩은 진열대 앞 (F3), 나머지(얼룩 포함)는 손님 쪽 바닥 아무 데나.
            // 얼룩을 나가는 손님 발밑에 두면 손님이 늘 계산대 앞에서 나가 같은 자리에만 생겼음 (11-20 C2) → 자리는 고르고, 말은 나가는 손님이 함
            bool spill = kind == CleanupKinds.FloorStain && customer != null && ShopLayout.InsideShop(customer.position);
            Vector3 around = kind == CleanupKinds.MessyPack ? PickMessySpot() : PickSpot();

            CleanupSpot spot = kind switch
            {
                CleanupKinds.Trash => SpawnTrash(around),
                CleanupKinds.MessyPack => SpawnMessyPacks(around),
                CleanupKinds.FloorStain => SpawnStain(around, spill ? customer : null),
                _ => null,
            };
            if (spot == null) return;
            // 생기는 순간 그 자리에서 소리 → 고개를 돌려 찾을 수 있게 (11-21 피드백 S5 · O2 "어디 있는지 잘 안 보임")
            Sfx.Play(kind switch
            {
                CleanupKinds.Trash => SoundId.TrashFall,
                CleanupKinds.MessyPack => SoundId.PackFall,
                _ => SoundId.LiquidSpill,
            }, spot.transform.position);
            spot.Completed += OnCompleted;
            active.Add(spot);
            spawnedToday++;
            quietStreak = 0;
        }

        // 손님 자리 대신: 플레이어가 지금 보는 쪽, 다른 청소 구역과 떨어진 바닥 (CleanupPlacement)
        Vector3 PickSpot()
        {
            if (placement == null) placement = CreatePlacement();
            placement.OutOfViewChance = outOfViewChance;
            placement.MinSeparation = minSeparation;
            placement.ViewHalfAngle = viewHalfAngle;

            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
            Vector3 eye = viewer != null ? viewer.position : new Vector3(0f, 1.6f, 0f);
            Vector3 forward = viewer != null ? viewer.forward : Vector3.forward;

            CollectActiveSpots();
            return placement.Pick(context.Random, eye, forward, activeSpots);
        }

        // 흐트러진 팩: 진열대 앞 바닥 띠에서, 계산대 눈높이에서 카운터 · 팩 테이블에 가리지 않는 자리 (못 찾으면 띠 안 마지막 후보)
        Vector3 PickMessySpot()
        {
            if (messyPlacement == null)
            {
                messyPlacement = new CleanupPlacement(MessyArea(messyBand)) { OutOfViewChance = 0f, ViewHalfAngle = 90f, MaxViewDistance = 10f };
                foreach (Vector3 q in ShopLayout.QueuePositions)
                    messyPlacement.Obstacles.Add(CleanupPlacement.Box(q, new Vector3(0.5f, 0f, 0.5f), 0.1f));
            }
            messyPlacement.MinSeparation = minSeparation;
            CollectActiveSpots();
            Vector3 p = default;
            for (int i = 0; i < VisibleTries; i++)
            {
                p = messyPlacement.Pick(context.Random, CounterEye, ShopLayout.StandPos - CounterEye, activeSpots);
                if (!Physics.Linecast(CounterEye, p + Vector3.up * 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return p;
            }
            return p;
        }

        /// <summary>진열대 앞 바닥 띠 (x, z): 진열대 폭, 앞면에서 band.x ~ band.y 떨어진 곳</summary>
        public static Rect MessyArea(Vector2 band)
        {
            float front = ShopLayout.StandPos.z - ShopLayout.StandSize.z / 2f;
            float halfW = ShopLayout.StandSize.x / 2f;
            return Rect.MinMaxRect(ShopLayout.StandPos.x - halfW, front - band.y, ShopLayout.StandPos.x + halfW, front - band.x);
        }

        void CollectActiveSpots()
        {
            activeSpots.Clear();
            foreach (CleanupSpot s in active)
                if (s != null && !s.IsDone) activeSpots.Add(s.transform.position);
        }

        // 손님 쪽 바닥(카운터 앞 ~ 앞벽)에서 진열대 · 손님 줄 자리를 뺀 곳
        static CleanupPlacement CreatePlacement()
        {
            const float wallMargin = 0.5f;
            float minX = ShopLayout.MinX + ShopLayout.WallHalf + wallMargin + 0.3f; // 왼쪽 벽장 · 서랍장 깊이
            float maxX = ShopLayout.MaxX - ShopLayout.WallHalf - wallMargin;
            float minZ = ShopLayout.CounterCenter.z + ShopLayout.CounterSize.z / 2f + 0.35f;
            float maxZ = ShopLayout.FrontZ - ShopLayout.WallHalf - wallMargin;
            var p = new CleanupPlacement(Rect.MinMaxRect(minX, minZ, maxX, maxZ));
            p.Obstacles.Add(CleanupPlacement.Box(ShopLayout.StandPos, ShopLayout.StandSize, 0.3f));
            // 손님 줄 자리: 손님 발밑에 가려지지 않게
            foreach (Vector3 q in ShopLayout.QueuePositions)
                p.Obstacles.Add(CleanupPlacement.Box(q, new Vector3(0.5f, 0f, 0.5f), 0.1f));
            return p;
        }

        CleanupKinds Available()
        {
            CleanupKinds k = CleanupKinds.None;
            if (bin != null && trashTemplates != null && trashTemplates.Length > 0) k |= CleanupKinds.Trash;
            if (rack != null && rack.InPlaceCount > 0) k |= CleanupKinds.MessyPack;
            if (mop == null) mop = FindAnyObjectByType<Broom>();
            if (mop != null) k |= CleanupKinds.FloorStain;
            return k;
        }

        const float MinTrashLength = 0.18f; // 키운 뒤 긴 변이 이보다 짧으면 이 길이까지 더 키움 (m)

        // 원본(꺼진 견본)의 메시 크기로 긴 변을 잼. 견본이 꺼져 있어 Renderer.bounds를 못 쓰므로 메시 범위 × 배율
        float SmallTrashBoost(TrashSpot template)
        {
            float longest = 0f;
            foreach (MeshFilter mf in template.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Vector3 size = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
                longest = Mathf.Max(longest, size.x, size.y, size.z);
            }
            float scaled = longest * trashScale;
            return scaled > 0.001f && scaled < MinTrashLength ? MinTrashLength / scaled : 1f;
        }

        CleanupSpot SpawnTrash(Vector3 around)
        {
            TrashSpot template = trashTemplates[context.Random.Range(0, trashTemplates.Length)];
            // PickSpot이 고른 바닥 자리 그대로
            // 크기를 키우면 가운데 원점 물건이 바닥에 묻히므로 높이도 같은 배율 (바닥 y = 0)
            Vector3 pos = new Vector3(around.x, template.transform.position.y * trashScale, around.z);
            Quaternion rot = Quaternion.Euler(0f, (float)context.Random.NextDouble() * 360f, 0f) * template.transform.rotation;
            TrashSpot trash = Instantiate(template, pos, rot, transform);
            float scale = trashScale * SmallTrashBoost(template); // 작은 컵 같은 것은 더 키움 (11-21 2차 F3)
            trash.transform.localScale = template.transform.localScale * scale; // 잡는 콜라이더도 같이 커짐
            trash.transform.position = new Vector3(pos.x, template.transform.position.y * scale, pos.z);
            trash.gameObject.SetActive(true);
            trash.Init(bin);
            context.Events.RaiseNotice("바닥에 쓰레기가 떨어졌어요. 주워서 카운터 오른쪽 쓰레기통에!");
            context.Events.RaiseHappened(GameMoment.TrashSpawned);
            return trash;
        }

        CleanupSpot SpawnMessyPacks(Vector3 around)
        {
            List<RackPack> scattered = rack.Scatter(around, packsPerMess, context.Random);
            if (scattered.Count == 0) return null;
            var go = new GameObject("MessyPacks");
            go.transform.SetParent(transform, false);
            go.transform.position = around; // 다른 청소 구역과 떨어뜨릴 때 이 자리를 씀
            var spot = go.AddComponent<MessyPackSpot>();
            spot.Init(scattered);
            context.Events.RaiseNotice("진열대 팩이 바닥에 떨어졌어요. 왼쪽 판매 진열대 팩 꽂이에 다시 꽂아 주세요!");
            context.Events.RaiseHappened(GameMoment.MessyPackSpawned);
            return spot;
        }

        CleanupSpot SpawnStain(Vector3 around, Transform spiller)
        {
            float d = Mathf.Lerp(stainDiameter.x, stainDiameter.y, (float)context.Random.NextDouble());
            if (customers == null) customers = FindAnyObjectByType<CustomerDirector>();
            StainSpot stain = StainSpot.Create(transform, new Vector3(around.x, 0f, around.z), d, mop, context.Random);
            stain.IsPaused = () => flow.IsPaused;
            stain.Customers = customers != null ? customers.Agents : null;
            CustomerAgent who = spiller != null ? spiller.GetComponent<CustomerAgent>() : null;
            if (who != null && who.Bubble != null) who.Bubble.Say(SpillLines[context.Random.Range(0, SpillLines.Length)], 2.4f);
            context.Events.RaiseNotice("바닥에 얼룩이 생겼어요. 카운터 오른쪽 대걸레로 문질러 주세요!");
            context.Events.RaiseHappened(GameMoment.StainSpawned);
            return stain;
        }

        void OnCompleted(CleanupSpot spot)
        {
            spot.Completed -= OnCompleted;
            if (flow.CurrentPhase != GamePhaseId.Business) return;
            context.Session.Ledger.cleanupsDone++;
            context.Events.RaiseHappened(spot is TrashSpot ? GameMoment.TrashBinned : spot is StainSpot ? GameMoment.StainCleaned : GameMoment.MessyPackFixed);
            int left = PendingCount;
            context.Events.RaiseNotice(left > 0
                ? $"청소 완료! 알바비 +{context.Economy.cleanupBonus} (남은 청소 {left}곳)"
                : $"청소 완료! 알바비 +{context.Economy.cleanupBonus}");
        }

        /// <summary>영업 끝: 남은 구역을 걷어 내고 치우지 못한 수를 돌려줍니다. 두 번 불러도 두 번째는 0.</summary>
        public int EndDay()
        {
            int missed = PendingCount;
            foreach (CleanupSpot s in active)
            {
                if (s == null) continue;
                s.Completed -= OnCompleted;
                s.Discard();
            }
            active.Clear();
            spawnedToday = quietStreak = 0;
            if (rack != null) rack.ResetAll();
            return missed;
        }
    }
}
