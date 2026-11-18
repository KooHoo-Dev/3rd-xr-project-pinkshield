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
        /// <summary>CardRevealer 인스펙터 값 (씬에 저장된 값을 그대로 쓰기 위해 CardRevealer에 둠).</summary>
        public struct Settings
        {
            public float CounterScale;      // 카운터에 놓인 카드 크기 배율
            public float FlySeconds;        // 더미까지 날아가는 시간
            public float PileThickness;     // 단일 팩: 한 장당 높이
            public float BoxPileThickness;  // 상자: 한 장당 높이 (50장이라 얇게)
            public float JitterAngle;
            public float JitterOffset;
            public int PileVisibleCards;    // 단일 팩: 실제 카드로 두는 수
            public int BoxPileVisible;      // 상자: 위쪽에 실제 카드로 두는 수
        }

        readonly MonoBehaviour host; // 보관 연출 기다리기(코루틴) · 덩어리 부모
        readonly CardRevealPool pool;
        readonly Func<Material> edgeMaterial;
        readonly Func<Settings> settings;
        readonly CounterPile<CardRevealView> ledger = new CounterPile<CardRevealView>(v => v.Card, v => v.IsNew, v => v.IsHeld);
        readonly List<CardRevealView> recycled = new List<CardRevealView>();
        readonly List<CardRevealView> stashing = new List<CardRevealView>(); // 보관함으로 날아가는 중
        Transform block;
        float blockThickness = 0.002f;

        /// <param name="edgeMaterial">덩어리에 입힐 카드 옆면 머티리얼 (없으면 기본)</param>
        /// <param name="settings">인스펙터 값을 쓸 때마다 읽음 (Play 중 값을 바꿔도 반영)</param>
        public CounterPileView(MonoBehaviour host, CardRevealPool pool, Func<Material> edgeMaterial, Func<Settings> settings)
        {
            this.host = host;
            this.pool = pool;
            this.edgeMaterial = edgeMaterial;
            this.settings = settings;
        }

        /// <summary>더미 맨 아래 카드 자리 (결과함 · 정보 판 위치 기준).</summary>
        public Pose BasePose { get; private set; }
        public bool HasCards => ledger.HasCards;
        public int CardCount => ledger.CardCount;
        public IReadOnlyList<CardRevealView> Cards => ledger.Pile;
        public bool Contains(CardRevealView v) => ledger.Contains(v);
        /// <summary>단일 팩 더미 index번째 카드 높이 (결과함 · 정보 판을 더미 위에 띄울 때).</summary>
        public float HeightAt(int index) => CardPileLayout.Height(index, settings().PileThickness);
        /// <summary>다음 카드가 놓일 높이 순번.</summary>
        public int NextIndex => ledger.NextIndex;
        /// <summary>테이블 카드 전체(덩어리 포함)와 그중 새 카드 수.</summary>
        public int CollectCards(List<CardData> cards) => ledger.CollectCards(cards);

        /// <summary>개봉을 시작할 때: 더미 자리와 덩어리 두께(상자는 얇게)를 정함.</summary>
        public void Begin(Pose basePose, bool isBox)
        {
            BasePose = basePose;
            Settings s = settings();
            blockThickness = isBox ? s.BoxPileThickness : s.PileThickness;
        }

        /// <summary>넘긴 카드를 더미 맨 위로 날려 보냄. 단일 팩은 모두 남기고, 상자는 위쪽 몇 장만 남기고 아래는 덩어리로.</summary>
        public void Send(CardRevealView v, bool isBox, float delay)
        {
            Settings s = settings();
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
            ledger.Compress(settings().PileVisibleCards, recycled);
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
        /// 연출이 끝나면 카드 오브젝트는 풀로 돌아갑니다.
        /// </summary>
        public List<CardData> Stash(ICardStash stash)
        {
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
            float scale = settings().CounterScale;
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
