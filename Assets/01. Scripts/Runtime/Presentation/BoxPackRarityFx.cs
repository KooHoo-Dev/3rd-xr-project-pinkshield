using System.Collections.Generic;
using CardShop.Interaction;
using CardShop.Logic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Presentation
{
    /// <summary>
    /// 상자 10팩 뜯기 장면에서 좋은 팩이 뜯기는 순간의 이펙트 (FIX D2 · DEC-029, DEC-004의 "상자 장면 힌트 없음"을 바꿈).
    /// BoxOpenEffect.PackTorn을 듣고, 그 팩의 연출 단계(RevealQueue.TierOf)에 따라
    /// 금빛(수퍼레어 · 유니크) / 무지개 두 겹 + 진동(레전더리) / 큰 금빛 + 긴 진동(갓팩) 폭죽을 팩 자리에서 터뜨립니다.
    /// 팩이 흔들리거나 튀어 오르는 움직임 · 빛 기둥은 BoxOpenEffect가 팩 위치를 매 프레임 정해서 넣지 않았습니다.
    /// </summary>
    public sealed class BoxPackRarityFx
    {
        const int BurstCount = 4; // 0.1초 간격으로 여러 팩이 뜯겨도 앞 폭죽이 끊기지 않게
        static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        static readonly Color Pink = new Color(1f, 0.45f, 0.85f);
        static readonly Color Cyan = new Color(0.4f, 0.95f, 1f);

        readonly Transform parent;
        readonly List<RevealBurst> bursts = new List<RevealBurst>(BurstCount);
        IReadOnlyList<PackResult> packs;
        int next;

        public BoxPackRarityFx(Transform parent) => this.parent = parent;

        /// <summary>이번 상자의 팩 목록 (BoxOpenEffect에 넘긴 순서와 같아야 함)</summary>
        public void Arm(IReadOnlyList<PackResult> packList) => packs = packList;

        public void Stop()
        {
            packs = null;
            foreach (RevealBurst b in bursts) b.Stop();
        }

        public void OnTorn(int index, Pose at)
        {
            if (packs == null || index < 0 || index >= packs.Count) return;
            Vector3 pos = at.position + at.rotation * Vector3.forward * 0.01f; // 팩 1cm 뒤 (앞면을 가리지 않게)
            XRBaseInputInteractor hand = HandFinder.Find(InteractorHandedness.Right);
            switch (RevealQueue.TierOf(packs[index]))
            {
                case BoxPackTier.Gold:
                    Next().Play(pos, at.rotation, Gold, 0.09f, 0.6f);
                    break;
                case BoxPackTier.Rainbow:
                    Next().Play(pos, at.rotation, Pink, 0.16f, 1.0f);
                    Next().Play(pos, at.rotation * Quaternion.Euler(0f, 0f, 22f), Cyan, 0.12f, 0.9f);
                    if (hand != null) hand.SendHapticImpulse(0.7f, 0.25f);
                    break;
                case BoxPackTier.God:
                    Next().Play(pos, at.rotation, Gold, 0.24f, 1.3f);
                    if (hand != null) hand.SendHapticImpulse(1f, 0.6f);
                    break;
            }
        }

        RevealBurst Next()
        {
            if (bursts.Count < BurstCount) bursts.Add(RevealBurst.Create(parent));
            RevealBurst b = bursts[next % bursts.Count];
            next++;
            return b;
        }
    }
}
