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

        GameContext context;
        readonly List<CleanupSpot> active = new List<CleanupSpot>();

        public int PendingCount
        {
            get { int n = 0; foreach (CleanupSpot s in active) if (s != null && !s.IsDone) n++; return n; }
        }

        void OnEnable()
        {
            if (flow == null) return;
            context = flow.GetComponent<GameContext>();
            flow.Cleanup = this;
            if (context != null) context.Events.CustomerLeft += OnCustomerLeft;
        }

        void OnDisable()
        {
            if (flow != null && ReferenceEquals(flow.Cleanup, this)) flow.Cleanup = null;
            if (context != null) context.Events.CustomerLeft -= OnCustomerLeft;
        }

        void OnCustomerLeft(Vector3 browsePosition)
        {
            if (flow.CurrentPhase != GamePhaseId.Business) return;

            CleanupKinds enabledToday = context.Campaign.GetDay(context.Session.Day).cleanupKinds;
            CleanupKinds kind = CleanupRules.Pick(enabledToday, Available(), context.Random);
            Vector3 around = browsePosition.sqrMagnitude > 0.01f
                ? browsePosition
                : ShopLayout.BrowsePositions[context.Random.Range(0, ShopLayout.BrowsePositions.Length)];

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
            // 손님 자리 주변, 진열대 반대쪽(-Z)으로 조금 떨어진 바닥
            float x = around.x + ((float)context.Random.NextDouble() - 0.5f) * 0.8f;
            float z = around.z - 0.15f - (float)context.Random.NextDouble() * 0.4f;
            Vector3 pos = new Vector3(x, template.transform.position.y, z);
            Quaternion rot = Quaternion.Euler(0f, (float)context.Random.NextDouble() * 360f, 0f) * template.transform.rotation;
            TrashSpot trash = Instantiate(template, pos, rot, transform);
            trash.gameObject.SetActive(true);
            trash.Init(bin);
            context.Events.RaiseNotice("바닥에 쓰레기가 떨어졌어요. 주워서 쓰레기통에!");
            return trash;
        }

        CleanupSpot SpawnMessyPacks(Vector3 around)
        {
            List<RackPack> scattered = rack.Scatter(around, packsPerMess, context.Random);
            if (scattered.Count == 0) return null;
            var go = new GameObject("MessyPacks");
            go.transform.SetParent(transform, false);
            var spot = go.AddComponent<MessyPackSpot>();
            spot.Init(rack, scattered);
            context.Events.RaiseNotice("진열대 팩이 바닥에 떨어졌어요. 팩 꽂이에 다시 꽂아 주세요!");
            return spot;
        }

        void OnCompleted(CleanupSpot spot)
        {
            spot.Completed -= OnCompleted;
            if (flow.CurrentPhase != GamePhaseId.Business) return;
            context.Session.Ledger.cleanupsDone++;
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
