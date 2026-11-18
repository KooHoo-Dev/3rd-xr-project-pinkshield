using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 영업 중 청소 구역 관리 (기획서 5-1 · 설계서 7-4, 창문 얼룩은 DEC-014로 제외: 바닥 쓰레기 · 흐트러진 팩 2종). 손님이 나갈 때마다 그날 켜진 종류(CampaignConfig.cleanupKinds) 중
    /// 하나를 만들고, 치우면 장부에 보너스 건수를 적습니다. 영업이 끝나면 BusinessPhase가 EndDay로 남은 구역을 걷어 내고
    /// 치우지 못한 수를 감점으로 적습니다. 청소 종류를 줄이려면 CampaignConfig의 cleanupKinds만 바꾸면 됩니다.
    /// </summary>
    public sealed class CleanupDirector : MonoBehaviour, ICleanupFloor
    {
        [SerializeField] GameFlow flow;

        [Header("바닥 쓰레기")]
        [SerializeField] TrashBin bin;
        [Tooltip("쓰레기 모양 원본 (꺼 둠). 하나를 골라 복제")]
        [SerializeField] TrashSpot[] trashTemplates;

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

        GameContext context;
        CleanupPlacement placement;
        Transform viewer;
        readonly List<CleanupSpot> active = new List<CleanupSpot>();
        readonly List<Vector3> activeSpots = new List<Vector3>();

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
            if (context != null) context.Events.CustomerLeft += OnCustomerLeft;
        }

        void OnDisable()
        {
            if (flow != null && ReferenceEquals(flow.Cleanup, this)) flow.Cleanup = null;
            if (context != null) context.Events.CustomerLeft -= OnCustomerLeft;
        }

        void OnCustomerLeft()
        {
            if (flow.CurrentPhase != GamePhaseId.Business) return;

            CleanupKinds enabledToday = context.Campaign.GetDay(context.Session.Day).cleanupKinds;
            CleanupKinds kind = CleanupRules.Pick(enabledToday, Available(), context.Random);
            if (kind == CleanupKinds.None) return;
            Vector3 around = PickSpot();

            CleanupSpot spot = kind switch
            {
                CleanupKinds.Trash => SpawnTrash(around),
                CleanupKinds.MessyPack => SpawnMessyPacks(around),
                _ => null,
            };
            if (spot == null) return;
            spot.Completed += OnCompleted;
            active.Add(spot);
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

            activeSpots.Clear();
            foreach (CleanupSpot s in active)
                if (s != null && !s.IsDone) activeSpots.Add(s.transform.position);
            return placement.Pick(context.Random, eye, forward, activeSpots);
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
            return k;
        }

        CleanupSpot SpawnTrash(Vector3 around)
        {
            TrashSpot template = trashTemplates[context.Random.Range(0, trashTemplates.Length)];
            // PickSpot이 고른 바닥 자리 그대로
            Vector3 pos = new Vector3(around.x, template.transform.position.y, around.z);
            Quaternion rot = Quaternion.Euler(0f, (float)context.Random.NextDouble() * 360f, 0f) * template.transform.rotation;
            TrashSpot trash = Instantiate(template, pos, rot, transform);
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

        void OnCompleted(CleanupSpot spot)
        {
            spot.Completed -= OnCompleted;
            if (flow.CurrentPhase != GamePhaseId.Business) return;
            context.Session.Ledger.cleanupsDone++;
            context.Events.RaiseHappened(spot is TrashSpot ? GameMoment.TrashBinned : GameMoment.MessyPackFixed);
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
            if (rack != null) rack.ResetAll();
            return missed;
        }
    }
}
