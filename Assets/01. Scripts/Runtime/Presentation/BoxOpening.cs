using System;
using System.Collections.Generic;
using CardShop.Core;
using CardShop.Logic;
using TMPro;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 상자 개봉의 "상자 전용" 부품 묶음 (FIX D2 때 CardRevealer에서 나눔, CLAUDE.md 400줄 규칙):
    /// 눈앞 카드 묶음 자리 · 10팩 뜯기 연출(BoxOpenEffect) · 좋은 팩 이펙트(BoxPackRarityFx) · "N / 10 팩"(BoxPackCounter) · 좋은 팩을 뒤로 보내는 순서.
    /// 개봉 진행 순서(뜯기 → 넘기기 → 결과)는 그대로 CardRevealer가 맡고, 이 클래스는 CardRevealer를 모릅니다. 부품은 처음 쓸 때 만듭니다.
    /// </summary>
    public sealed class BoxOpening
    {
        /// <summary>인스펙터 묶음 (CardRevealer의 "상자 일괄 개봉"). 기본값 = 나누기 전 씬 값</summary>
        [Serializable]
        public sealed class Settings
        {
            [Tooltip("상자 카드 묶음 자리: 머리에서 정면으로 (m). 단일 팩을 눈앞에 들었을 때(TearGestureOpen)와 같게")]
            public float StackDistance = 0.45f;
            public float StackDrop = 0.08f;
            [Tooltip("상자 팩 뜯는 소리. 비워 두면 SoundBank의 PackTear")]
            public AudioClip TearSound;
        }

        const int PrewarmPacks = 10;

        readonly Transform parent;
        readonly Settings settings;
        readonly Func<TMP_FontAsset> font;
        BoxOpenEffect effect;
        BoxPackRarityFx rarityFx;
        BoxPackCounter counter;

        public BoxOpening(Transform parent, Settings settings, Func<TMP_FontAsset> font)
        {
            this.parent = parent;
            this.settings = settings;
            this.font = font;
        }

        /// <summary>좋은 팩일수록 뒤로 (G19 "좋은 건 마지막", DEC-029). 데이터(보유 목록)는 바뀌지 않고 보여 주는 순서만</summary>
        public static List<PackResult> Order(PackResult[] packs) => RevealQueue.GoodLast(packs);

        /// <summary>단일 팩을 눈앞에 들었을 때와 같은 곳(머리 정면, 조금 아래). 카메라가 없으면 anchor 위</summary>
        public Pose StackPose(Transform anchor)
        {
            Camera cam = Camera.main;
            if (cam != null) return HeadPose.InFront(cam.transform, settings.StackDistance, settings.StackDrop);
            Vector3 flat = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            return new Pose(anchor.position + Vector3.up * 0.15f, Quaternion.LookRotation(flat.normalized, Vector3.up));
        }

        public void WarmUp() => Effect();

        /// <summary>10팩 뜯기 연출을 시작하고 걸리는 시간(초)을 돌려줍니다. 좋은 팩은 뜯기는 순간 이펙트</summary>
        public float PlayOpen(IReadOnlyList<PackResult> packs, Pose stack)
        {
            Transform head = Camera.main != null ? Camera.main.transform : null;
            Vector3 headPos = head != null ? head.position : stack.position - stack.rotation * Vector3.forward * settings.StackDistance;
            BoxOpenEffect e = Effect();
            rarityFx.Arm(packs);
            return e.Play(packs.Count, headPos, stack.rotation * Vector3.forward, stack, settings.TearSound);
        }

        public void ShowCounter(int pack, int total, Pose stack) =>
            (counter != null ? counter : counter = BoxPackCounter.Create(parent, font())).Show(pack, total, stack);

        public void HideCounter() { if (counter != null) counter.Hide(); }

        /// <summary>뜯기 · 이펙트 · 숫자를 모두 멈춤 (전체 정리)</summary>
        public void Stop()
        {
            if (effect != null) effect.Stop();
            rarityFx?.Stop();
            HideCounter();
        }

        BoxOpenEffect Effect()
        {
            if (effect != null) return effect;
            effect = BoxOpenEffect.Create(parent, PrewarmPacks);
            rarityFx = new BoxPackRarityFx(parent);
            effect.PackTorn += rarityFx.OnTorn;
            return effect;
        }
    }
}
