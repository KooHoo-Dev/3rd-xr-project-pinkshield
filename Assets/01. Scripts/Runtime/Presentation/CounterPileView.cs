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
            [Tooltip("쌓아 둘 수 있는 카드 수(덩어리 포함). 넘으면 쌓인 카드를 보관함으로 옮기고 새로 쌓음")]
            [Min(10)] public int MaxCards = 60;
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
        float blockThickness = 0.002f;
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
        public bool HasCards => ledger.HasCards;
        /// <summary>더 쌓을 수 없을 만큼 찼음 (새 팩을 열기 전에 보관함으로 옮김).</summary>
        public bool IsFull => ledger.CardCount >= settings.MaxCards;
        /// <summary>카드가 더미까지 날아가는 시간 (마지막 카드가 내려앉기를 기다릴 때).</summary>
        public float FlySeconds => settings.FlySeconds;
        public IReadOnlyList<CardRevealView> Cards => ledger.Pile;
        public bool Contains(CardRevealView v) => ledger.Contains(v);
        /// <summary>단일 팩 더미 index번째 카드 높이 (결과함 · 정보 판을 더미 위에 띄울 때).</summary>
        public float HeightAt(int index) => CardPileLayout.Height(index, settings.PileThickness);
        /// <summary>다음 카드가 놓일 높이 순번.</summary>
        public int NextIndex => ledger.NextIndex;
        /// <summary>테이블 카드 전체(덩어리 포함)와 그중 새 카드 수.</summary>
        public int CollectCards(List<CardData> cards) => ledger.CollectCards(cards);

        /// <summary>개봉을 시작할 때: 카운터 기준점에서 더미 자리를 정하고, 덩어리 두께(상자는 얇게)를 고름.</summary>
        public void Begin(Transform anchor, bool isBox)
        {
            BasePose = new Pose(anchor.position + settings.Offset, CardPileLayout.FlatRotation(anchor.forward));
            blockThickness = isBox ? settings.BoxPileThickness : settings.PileThickness;
        }

        /// <summary>넘긴 카드를 더미 맨 위로 날려 보냄. 단일 팩은 모두 남기고, 상자는 위쪽 몇 장만 남기고 아래는 덩어리로.</summary>
        public void Send(CardRevealView v, bool isBox, float delay)
        {
            Settings s = settings;
            int index = ledger.Place(v, isBox, Time.time + delay + s.FlySeconds);
            Pose p = CardPileLayout.GetPose(index, BasePose, isBox ? s.BoxPileThickness : s.PileThickness, s.JitterAngle, s.JitterOffset);
            v.FlyTo(p.position, p.rotation, s.FlySeconds, s.CounterScale, grabbableOnArrive: false, delay: delay);
            if (!isBox) return;
            ledger.TrimBox(Time.time, s.BoxPileVisible, recycled);
            RecycleBuffered();
            UpdateBlock();
        }

        /// <summary>건너뛴 상자 카드: 오브젝트 없이 덩어리 높이로만 셈.</summary>
        public void CountSkipped(int count)
        {
            ledger.CountSkipped(count);
            UpdateBlock();
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
        public void Reset() => ledger.Reset();

        /// <summary>
        /// 더미를 보관함으로 보냄. 덩어리 카드까지 포함한 카드 목록(아래부터)을 돌려주고, 보낼 카드가 없으면 null.
        /// 연출이 끝나면 카드 오브젝트는 풀로 돌아갑니다. (보관함 연출 = 더미가 위로 날아가 사라짐, 바인더 칸 반짝임은 BinderView가 PileStashed로)
        /// </summary>
        public List<CardData> Stash()
        {
            stash ??= new FlyAwayCardStash(settings.StashRise, settings.StashSeconds, settings.StashStagger);
            if (!ledger.HasCards) return null;
            var batch = new List<CardRevealView>();
            var cards = new List<CardData>();
            ledger.TakeAll(batch, cards);
            HideBlock();
            foreach (CardRevealView v in batch)
            {
                pool.Detach(v);
                stashing.Add(v);
            }
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
            float h = CardPileLayout.Height(blockTop - 1, blockThickness) + cardT;
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
