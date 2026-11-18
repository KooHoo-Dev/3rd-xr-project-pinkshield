using System;
using System.Collections.Generic;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 눈앞 카드 묶음 (시연 피드백 A4 · B3): 공개 대기열(<see cref="RevealItem"/>)의 맨 앞 몇 장만 실제 카드로 만들어 겹쳐 둡니다(Quest 성능).
    /// 맨 앞 카드를 넘기면 뒤 카드들이 한 칸씩 당겨지고, 빈자리는 풀에서 꺼내 채웁니다. 건너뛰면 남은 카드를 카운터 더미로 보냅니다.
    /// 카드는 늘 앞면으로 쌓습니다 (뒤집기 연출은 DEC-004 수정으로 뺌). 묶음 뒤 카드는 앞 카드에 가려 보이지 않습니다.
    /// CardRevealer(개봉 진행)가 부르고, 이 클래스는 CardRevealer를 모릅니다.
    /// </summary>
    public sealed class RevealStack
    {
        /// <summary>인스펙터에서 조정하는 값 (CardRevealer 인스펙터에 이 묶음으로 보임).</summary>
        [Serializable]
        public sealed class Settings
        {
            [Tooltip("묶음에서 카드 사이 간격 (m)")]
            public float Spacing = 0.003f;
            [Tooltip("상자: 눈앞 카드 묶음 크기 배율 (팩 크기 배율과 같게)")]
            public float Scale = 1.5f;
            [Tooltip("단일 팩: 카드 묶음 크기 = 눈앞 팩 크기 × 이 값. 카드(63×88mm)가 팩 몸통(75×102mm) 안에 맞게")]
            public float PackFitRatio = 1.1f;
            [Tooltip("단일 팩: 팩 크기로 시작한 카드 묶음이 위로 솟으며 이 배율만큼 커짐 (실기기 피드백: 개봉 카드가 다시 작아 보임 → 1.5배)")]
            public float RevealGrow = 1.5f;
            [Tooltip("단일 팩: 뜯긴 팩에서 카드 묶음이 위로 솟는 거리 (m)와 시간 (초)")]
            public float Rise = 0.07f;
            public float RiseSeconds = 0.45f;
            [Tooltip("실제로 만들어 두는 카드 수 (맨 앞부터). 나머지는 넘길 때마다 풀에서 꺼냄 (Quest 성능)")]
            [Min(1)] public int Depth = 3;
            [Tooltip("앞 카드가 빠진 뒤 뒤 카드들이 앞으로 당겨지는 시간 (초)")]
            public float RestackSeconds = 0.12f;
            [Tooltip("상자 · 건너뛴 뒤: 카드 묶음이 떠오르는 시간 (초)")]
            public float PopInSeconds = 0.3f;
            [Tooltip("건너뛴 카드들이 더미로 날아갈 때 카드 사이 시차 (초, 단일 팩)")]
            public float SkipFlyStagger = 0.04f;
        }

        readonly CardRevealPool pool;
        readonly CounterPileView pile;
        readonly Settings settings;
        readonly List<CardRevealView> views = new List<CardRevealView>(); // views[k] = 대기열의 (지금 위치 + k)번째

        public RevealStack(CardRevealPool pool, CounterPileView pile, Settings settings)
        {
            this.pool = pool;
            this.pile = pile;
            this.settings = settings;
        }

        /// <summary>묶음 자리 (앞면 -Z가 플레이어 쪽).</summary>
        public Pose Pose { get; private set; }
        /// <summary>이번 개봉의 카드 크기 배율 (단일 팩은 팩 크기에 맞춤, 상자는 고정).</summary>
        public float Scale { get; private set; }
        public bool IsEmpty => views.Count == 0;
        /// <summary>지금 공개 중인 맨 앞 카드.</summary>
        public CardRevealView Front => views[0];

        /// <summary>떠오르며 나타나는 데 걸리는 시간 (상자 묶음이 다 떠오르기를 기다릴 때).</summary>
        public float PopInSeconds => settings.PopInSeconds;

        /// <summary>건너뛴 카드 cardCount장이 시차를 두고 출발할 때 마지막 카드가 늦게 출발하는 시간.</summary>
        public float SkipDelay(int cardCount) => Mathf.Max(0, cardCount - 1) * settings.SkipFlyStagger;

        /// <summary>
        /// 개봉을 시작할 때: 묶음 자리와 카드 크기를 정함 (남은 카드는 없어야 함).
        /// packScale이 있으면(단일 팩) 눈앞 팩 크기에 맞추고, 없으면(상자) 고정 크기.
        /// </summary>
        public void Begin(Pose pose, float? packScale)
        {
            Pose = pose;
            Scale = packScale.HasValue ? packScale.Value * settings.PackFitRatio : settings.Scale;
        }

        /// <summary>
        /// 대기열 queue[from..]의 맨 앞 몇 장을 실제 카드로 채움. 남아 있던 카드는 한 칸씩 앞으로 당김.
        /// popIn이면 조금 아래에서 작게 시작해 떠오름.
        /// </summary>
        public void Fill(List<PackResult> packs, List<RevealItem> queue, int from, bool popIn)
        {
            Settings s = settings;
            Vector3 back = Pose.rotation * Vector3.forward;
            for (int k = 0; k < views.Count; k++)
                views[k].FlyTo(Pose.position + back * (s.Spacing * k), Pose.rotation, s.RestackSeconds, Scale,
                    arcHeight: 0f, grabbableOnArrive: false);

            while (views.Count < s.Depth && from + views.Count < queue.Count)
            {
                RevealItem item = queue[from + views.Count];
                PackResult p = packs[item.Pack];
                Vector3 pos = Pose.position + back * (s.Spacing * views.Count);
                Vector3 start = popIn ? pos - (Pose.rotation * Vector3.up) * 0.03f : pos;
                CardRevealView v = pool.Show(p.Cards[item.Card], p.IsNew[item.Card], start, Pose.rotation, popIn ? Scale * 0.6f : Scale);
                v.SetGrabbable(false);
                if (popIn) v.FlyTo(pos, Pose.rotation, s.PopInSeconds, Scale, arcHeight: 0f, grabbableOnArrive: false);
                views.Add(v);
            }
        }

        /// <summary>단일 팩: 뜯긴 팩에서 묶음이 위로 솟으며 커짐. 이후 채우는 카드도 이 크기 · 자리. 걸리는 시간(초)을 돌려줍니다.</summary>
        public float Rise()
        {
            Vector3 up = Pose.rotation * Vector3.up * settings.Rise;
            Scale *= settings.RevealGrow;
            foreach (CardRevealView v in views)
                v.FlyTo(v.transform.position + up, Pose.rotation, settings.RiseSeconds, Scale, arcHeight: 0f, grabbableOnArrive: false);
            Pose = new Pose(Pose.position + up, Pose.rotation);
            return settings.RiseSeconds;
        }

        /// <summary>맨 앞 카드를 묶음에서 뺌 (더미로 보내는 것은 부르는 쪽).</summary>
        public CardRevealView TakeFront()
        {
            CardRevealView v = views[0];
            views.RemoveAt(0);
            return v;
        }

        /// <summary>
        /// 건너뛰기 (DEC-004): queue[from..]의 남은 카드를 전 등급 건너뛰고, 끝까지 보여 줄 갓팩 카드만 담은 새 대기열을 돌려줍니다 (DEC-008).
        /// 단일 팩은 남은 카드가 모두 더미로 날아가고(결과함에 5장이 다 있어야 하므로), 상자는 더미 덩어리 카드로만 셉니다(오브젝트 없음).
        /// </summary>
        public List<RevealItem> Skip(List<PackResult> packs, List<RevealItem> queue, int from, bool isBox)
        {
            Settings s = settings;
            List<RevealItem> kept = RevealQueue.GodOnly(packs, queue, from);
            Vector3 back = Pose.rotation * Vector3.forward;
            int flyIndex = 0;
            for (int k = from; k < queue.Count; k++)
            {
                int slot = k - from;
                CardRevealView v = slot < views.Count ? views[slot] : null;
                RevealItem item = queue[k];
                if (RevealQueue.IsGodPack(packs[item.Pack]))
                {
                    if (v != null) pool.Recycle(v); // 갓팩 카드는 마지막에 다시 하나씩 떠오르며 공개됨
                    continue;
                }
                if (isBox)
                {
                    if (v != null) pool.Recycle(v);
                    PackResult sp = packs[item.Pack];
                    pile.CountSkipped(sp.Cards[item.Card], sp.IsNew[item.Card]);
                    continue;
                }
                if (v == null)
                {
                    PackResult p = packs[item.Pack];
                    v = pool.Show(p.Cards[item.Card], p.IsNew[item.Card], Pose.position + back * (s.Spacing * s.Depth), Pose.rotation, Scale);
                    v.SetGrabbable(false);
                }
                pile.Send(v, false, flyIndex++ * s.SkipFlyStagger);
            }
            views.Clear();
            return kept;
        }

        /// <summary>남은 카드를 모두 풀로 (전체 정리 · 상자 결과판으로 넘어갈 때).</summary>
        public void Clear()
        {
            foreach (CardRevealView v in views) pool.Recycle(v);
            views.Clear();
        }
    }
}
