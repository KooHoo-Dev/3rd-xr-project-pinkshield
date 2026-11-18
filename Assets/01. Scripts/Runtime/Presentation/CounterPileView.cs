using System;
using System.Collections;
using System.Collections.Generic;
using CardShop.Cards;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 카운터 카드 더미의 모양 (시연 피드백 B1). 장부(<see cref="CounterPile{T}"/>)가 정한 순번대로 카드를 날려 쌓고,
    /// 오래된 카드 · 상자 아래쪽 카드는 풀로 돌려보낸 뒤 카드 옆면 색 덩어리 하나로 보여 줍니다(Quest 성능).
    /// 퇴근 때는 더미를 보관함(<see cref="ICardStash"/>)으로 보내고, 연출이 끝나면 카드를 풀로 거둡니다.
    /// 한 더미가 ColumnCards장(기본 50)이 되면 다음 팩부터 옆에 새 더미를 쌓습니다 (11-19 피드백: 가득 차도 사라지지 않게).
    /// 다 찬 더미는 맨 위 카드 한 장 + 덩어리만 남겨 성능을 아낍니다.
    /// CardRevealer(개봉 진행)가 부르고, 이 클래스는 CardRevealer를 모릅니다.
    /// </summary>
    public sealed class CounterPileView
    {
        /// <summary>인스펙터에서 조정하는 값 (CardRevealer 인스펙터에 이 묶음으로 보임).</summary>
        [Serializable]
        public sealed class Settings
        {
            [Tooltip("더미 맨 아래 카드 자리: 기준점(anchor)에서 월드 좌표로 얼마나 떨어졌는지. 기본은 카운터 윗면 바로 위")]
            public Vector3 Offset = new Vector3(0f, -0.199f, 0f);
            [Tooltip("카운터에 놓인 카드 크기 배율 (시연 피드백 A1: 카드가 작아 보임)")]
            public float CounterScale = 1.3f;
            [Tooltip("넘긴 카드가 더미까지 날아가는 시간 (초)")]
            public float FlySeconds = 0.35f;
            [Tooltip("단일 팩: 카드 한 장이 쌓일 때마다 높아지는 정도 (m). 카드 두께 1.2mm × 크기 배율보다 살짝 크게")]
            public float PileThickness = 0.002f;
            [Tooltip("상자: 카드 한 장당 높이 (m). 50장이 쌓이므로 단일 팩보다 얇게")]
            public float BoxPileThickness = 0.0008f;
            [Tooltip("장마다 비뚤어지는 최대 각도 (도)")]
            public float JitterAngle = 7f;
            [Tooltip("장마다 어긋나는 최대 거리 (m)")]
            public float JitterOffset = 0.004f;
            [Tooltip("단일 팩: 실제 카드 오브젝트로 두는 수 (최근 것부터). 더 오래된 카드는 옆면 덩어리 하나로 줄여 성능을 아낌")]
            [Min(5)] public int PileVisibleCards = 15;
            [Tooltip("상자: 위쪽에 실제 카드로 남겨 두는 수. 그 아래는 덩어리로 표시")]
            [Min(1)] public int BoxPileVisible = 3;
            [Header("더미 여러 개 (11-19 피드백: 가득 차면 옆에 새 더미)")]
            [Tooltip("한 더미에 쌓는 카드 수. 이만큼 차면 다음 팩부터 옆에 새 더미 (상자 50장은 더미 하나)")]
            [Min(10)] public int ColumnCards = 50;
            [Tooltip("더미 사이 간격 (m). +면 플레이어 오른쪽(봉투 쪽)으로, −면 왼쪽으로 놓임")]
            public float ColumnSpacing = 0.1f;
            [Tooltip("더미 최대 개수. 다 차면 마지막 더미에 얇게 계속 쌓음")]
            [Min(1)] public int MaxColumns = 4;
            [Header("보관함 연출 (더미가 위로 날아가 사라짐)")]
            public float StashRise = 0.5f;
            public float StashSeconds = 0.45f;
            public float StashStagger = 0.05f;
        }

        readonly MonoBehaviour host; // 보관 연출 기다리기(코루틴) · 덩어리 부모
        readonly CardRevealPool pool;
        readonly Func<Material> edgeMaterial;
        readonly Settings settings;
        readonly CounterPile<CardRevealView> ledger = new CounterPile<CardRevealView>(v => v.Card, v => v.IsNew, v => v.IsHeld);
        readonly List<CardRevealView> recycled = new List<CardRevealView>();
        readonly List<CardRevealView> stashing = new List<CardRevealView>(); // 보관함으로 날아가는 중
        Transform block;
        // 다 찬 옆 더미들: 덩어리 + 맨 위 카드 + 카드 목록 (퇴근 때 함께 보관함으로)
        sealed class Column
        {
            public Transform Block;
            public readonly List<CardRevealView> Cards = new List<CardRevealView>();
            public readonly List<CardData> All = new List<CardData>();
        }
        readonly List<Column> columns = new List<Column>();
        Transform anchorRef;
        // 이 순번 아래 카드는 상자 두께(얇게)로 쌓임. 상자를 넘기는 동안은 전부, 끝나면 상자 카드 수만큼 (그 위 단일 팩은 보통 두께)
        int thinCount;
        ICardStash stash;

        /// <param name="edgeMaterial">덩어리에 입힐 카드 옆면 머티리얼 (없으면 기본)</param>
        /// <param name="settings">인스펙터 묶음을 그대로 참조 (Play 중 값을 바꿔도 반영)</param>
        public CounterPileView(MonoBehaviour host, CardRevealPool pool, Func<Material> edgeMaterial, Settings settings)
        {
            this.host = host;
            this.pool = pool;
            this.edgeMaterial = edgeMaterial;
            this.settings = settings;
        }

        /// <summary>더미 맨 아래 카드 자리 (결과함 · 정보 판 위치 기준).</summary>
        public Pose BasePose { get; private set; }
        public bool HasCards => ledger.HasCards || columns.Count > 0;
        /// <summary>지금(맨 오른쪽) 더미에 카드가 있음.</summary>
        public bool CurrentHasCards => ledger.HasCards;
        /// <summary>지금 더미가 다 참 (다음 팩은 옆 새 더미로).</summary>
        public bool ColumnFull => ledger.CardCount >= settings.ColumnCards;
        /// <summary>옆에 새 더미를 놓을 자리가 남음.</summary>
        public bool CanAddColumn => columns.Count + 1 < settings.MaxColumns;
        /// <summary>카드가 더미까지 날아가는 시간 (마지막 카드가 내려앉기를 기다릴 때).</summary>
        public float FlySeconds => settings.FlySeconds;
        public IReadOnlyList<CardRevealView> Cards => ledger.Pile;
        public bool Contains(CardRevealView v)
        {
            if (ledger.Contains(v)) return true;
            foreach (Column c in columns) if (c.Cards.Contains(v)) return true;
            return false;
        }
        /// <summary>더미 index번째 카드 높이 (아래쪽 상자 카드는 얇게, 그 위 단일 팩 카드는 보통 두께).</summary>
        public float HeightAt(int index) =>
            CardPileLayout.Height(index, thinCount, settings.BoxPileThickness, settings.PileThickness, settings.ColumnCards);
        /// <summary>다음 카드가 놓일 높이 순번.</summary>
        public int NextIndex => ledger.NextIndex;
        /// <summary>테이블 카드 전체(덩어리 포함)와 그중 새 카드 수.</summary>
        public int CollectCards(List<CardData> cards)
        {
            foreach (Column c in columns) cards.AddRange(c.All);
            return ledger.CollectCards(cards); // 새 카드 수는 지금 더미 것만 (옆 더미는 이미 지난 팩)
        }

        /// <summary>개봉을 시작할 때: 카운터 기준점에서 더미 자리를 정함. 상자는 빈 카운터에서 시작하고(RevealBox가 먼저 보관) 얇게 쌓음.</summary>
        public void Begin(Transform anchor, bool isBox)
        {
            anchorRef = anchor;
            BasePose = ColumnPose(columns.Count);
            if (isBox) thinCount = int.MaxValue;
        }

        // n번째 더미 바닥 자리: 기준점에서 카운터 가로 방향으로 ColumnSpacing씩
        Pose ColumnPose(int n)
        {
            Vector3 side = Vector3.ProjectOnPlane(anchorRef.right, Vector3.up).normalized;
            return new Pose(anchorRef.position + settings.Offset + side * (settings.ColumnSpacing * n), CardPileLayout.FlatRotation(anchorRef.forward));
        }

        /// <summary>
        /// 지금 더미를 다 찬 더미로 굳히고, 다음 카드는 옆 새 더미에 쌓음. 굳힌 더미는 맨 위 카드 한 장 + 덩어리만 남김 (Quest 성능).
        /// 손에 든 카드는 줄이지 않고 그대로 굳힌 더미에 남습니다.
        /// </summary>
        public void StartNewColumn()
        {
            if (!ledger.HasCards) return;
            ledger.Compress(1, recycled);
            RecycleBuffered();
            UpdateBlock();

            var col = new Column { Block = block };
            block = null; // 새 더미는 새 덩어리
            ledger.TakeAll(col.Cards, col.All);
            columns.Add(col);
            thinCount = 0;
            if (anchorRef != null) BasePose = ColumnPose(columns.Count);
        }

        /// <summary>넘긴 카드를 더미 맨 위로 날려 보냄. 단일 팩은 모두 남기고, 상자는 위쪽 몇 장만 남기고 아래는 덩어리로.</summary>
        public void Send(CardRevealView v, bool isBox, float delay)
        {
            Settings s = settings;
            int index = ledger.Place(v, isBox, Time.time + delay + s.FlySeconds);
            Pose p = CardPileLayout.GetPoseAt(index, BasePose, HeightAt(index), s.JitterAngle, s.JitterOffset);
            v.FlyTo(p.position, p.rotation, s.FlySeconds, s.CounterScale, grabbableOnArrive: false, delay: delay);
            if (!isBox) return;
            ledger.TrimBox(Time.time, s.BoxPileVisible, recycled);
            RecycleBuffered();
            UpdateBlock();
        }

        /// <summary>건너뛴 상자 카드: 오브젝트 없이 덩어리 카드로 셈.</summary>
        public void CountSkipped(CardData card, bool isNew)
        {
            ledger.CountSkipped(card, isNew);
            UpdateBlock();
        }

        /// <summary>
        /// 상자를 다 넘김: 상자 더미를 단일 팩 더미처럼 카운터에 남김 (11-19 플레이 피드백 B3).
        /// 위쪽 실제 카드는 잡을 수 있고, 이어서 여는 팩은 상자 더미 바로 위에(보통 두께로) 쌓이며, 퇴근 때 함께 보관함으로.
        /// </summary>
        public void SettleBox()
        {
            ledger.SettleBox();
            thinCount = ledger.NextIndex;
            UpdateBlock();
            foreach (CardRevealView v in ledger.Pile) v.SetGrabbable(true);
        }

        /// <summary>단일 팩 결과 때: 실제 카드로 두는 수를 넘으면 오래된 카드부터 덩어리로 (손에 든 카드는 건드리지 않음).</summary>
        public void Compress()
        {
            ledger.Compress(settings.PileVisibleCards, recycled);
            RecycleBuffered();
            UpdateBlock();
        }

        /// <summary>상자 카드(실제 카드 + 덩어리)를 치움. 단일 팩 더미는 그대로. keepBlock이 아니면 덩어리도 숨김.</summary>
        public void ClearBox(bool keepBlock)
        {
            ledger.ClearBox(recycled, keepBlock);
            RecycleBuffered();
            if (!keepBlock) HideBlock();
        }

        /// <summary>장부를 비움 (카드 오브젝트는 부르는 쪽이 이미 풀로 돌려보낸 뒤).</summary>
        public void Reset()
        {
            ledger.Reset();
            thinCount = 0;
            ClearColumns(); // 카드 오브젝트는 부르는 쪽이 이미 풀로 돌려보냄
        }

        void ClearColumns()
        {
            foreach (Column c in columns)
                if (c.Block != null) UnityEngine.Object.Destroy(c.Block.gameObject);
            columns.Clear();
            if (anchorRef != null) BasePose = ColumnPose(0);
        }

        /// <summary>
        /// 더미를 보관함으로 보냄. 덩어리 카드까지 포함한 카드 목록(아래부터)을 돌려주고, 보낼 카드가 없으면 null.
        /// 연출이 끝나면 카드 오브젝트는 풀로 돌아갑니다. (보관함 연출 = 더미가 위로 날아가 사라짐, 바인더 칸 반짝임은 BinderView가 PileStashed로)
        /// </summary>
        public List<CardData> Stash()
        {
            stash ??= new FlyAwayCardStash(settings.StashRise, settings.StashSeconds, settings.StashStagger);
            if (!HasCards) return null;
            var batch = new List<CardRevealView>();
            var cards = new List<CardData>();
            foreach (Column c in columns) // 옆 더미부터 (먼저 연 카드)
            {
                batch.AddRange(c.Cards);
                cards.AddRange(c.All);
            }
            ClearColumns();
            ledger.TakeAll(batch, cards);
            thinCount = 0;
            HideBlock();
            foreach (CardRevealView v in batch)
            {
                pool.Detach(v);
                stashing.Add(v);
            }
            if (batch.Count > 0) Sfx.Play(SoundId.PileStash, batch[batch.Count - 1].transform.position);
            float seconds = stash.Send(batch);
            host.StartCoroutine(RestAfter(batch, seconds));
            return cards;
        }

        /// <summary>보관함으로 날아가는 중인 카드를 바로 거둠 (전체 정리).</summary>
        public void StopStashing()
        {
            foreach (CardRevealView v in stashing) pool.Rest(v);
            stashing.Clear();
        }

        IEnumerator RestAfter(List<CardRevealView> batch, float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
            foreach (CardRevealView v in batch)
                if (stashing.Remove(v)) pool.Rest(v); // 이미 StopStashing으로 거둬 갔으면 건너뜀
        }

        void RecycleBuffered()
        {
            foreach (CardRevealView v in recycled) pool.Recycle(v);
            recycled.Clear();
        }

        // 카드 옆면 색 상자 하나로 더미 아래쪽을 표시 (카드 50장을 다 두지 않음)
        void UpdateBlock()
        {
            int blockTop = ledger.BlockTop;
            if (blockTop <= 0) { HideBlock(); return; }
            float scale = settings.CounterScale;
            Transform b = EnsureBlock();
            float cardT = CardView.Thickness * scale;
            float h = HeightAt(blockTop - 1) + cardT;
            b.gameObject.SetActive(true);
            b.SetPositionAndRotation(BasePose.position + Vector3.up * (h * 0.5f - cardT * 0.5f), BasePose.rotation);
            // 눕힌 카드 기준: 로컬 X = 가로, Y = 세로, Z = 두께(아래쪽)
            b.localScale = new Vector3(CardView.Width * scale * 0.98f, CardView.Height * scale * 0.98f, h);
        }

        void HideBlock()
        {
            if (block != null) block.gameObject.SetActive(false);
        }

        Transform EnsureBlock()
        {
            if (block != null) return block;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BoxPileBlock";
            go.transform.SetParent(host.transform, false);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>()); // 보기만 함 (잡기 · 물리 X)
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material edge = edgeMaterial();
            if (edge != null) r.sharedMaterial = edge;
            block = go.transform;
            return block;
        }
    }
}
