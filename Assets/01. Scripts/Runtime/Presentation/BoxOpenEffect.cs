using System;
using System.Collections;
using System.Collections.Generic;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 상자 10팩 일괄 개봉 연출 (시연 피드백 B3). 판정은 이미 끝났고 보여 주기만 합니다(건너뛰어도 데이터는 같음).
    /// ① 카드 묶음 자리에서 팩 10개가 눈앞 아치로 펼쳐짐 ② 왼쪽부터 0.12초 간격으로 윗띠가 뜯겨 날아가고 틈에서 빛줄기
    ///    (특수 팩 = 갓팩·레전더리 확정은 금빛 + 더 긴 빛줄기 + 다른 효과음 칸) ③ 팩들이 카드 묶음 자리로 모이며 사라짐.
    /// 실제 팩 프리팹(Rigidbody · XRI · TMP)이 아니라 판(스프라이트) 5장짜리 시각용 팩 10개를 재사용합니다(Quest 성능).
    /// 단일 팩 뜯김(PackTearEffect)의 움직임을 줄여 같은 느낌으로 맞췄습니다.
    /// </summary>
    public sealed class BoxOpenEffect : MonoBehaviour
    {
        // 팩 프리팹(PackPrefabBuilder)과 같은 크기 · 색
        const float W = 0.075f, H = 0.13f, SealH = 0.014f;
        static readonly Color FrontColor = new Color(1f, 0.55f, 0.72f);
        static readonly Color BackColor = new Color(0.36f, 0.27f, 0.55f);
        static readonly Color SealColor = new Color(0.78f, 0.74f, 0.9f);

        [Header("아치 배치")]
        [Tooltip("머리에서 팩까지 거리 (m)")]
        [SerializeField] float archRadius = 0.5f;
        [Tooltip("양 끝 팩이 정면에서 벌어지는 각도 (도)")]
        [SerializeField] float archHalfAngle = 55f;
        [Tooltip("눈높이보다 아래로 (m)")]
        [SerializeField] float archDrop = 0.12f;
        [Tooltip("가운데가 양 끝보다 높은 정도 (m)")]
        [SerializeField] float archLift = 0.05f;
        [SerializeField] float packScale = 1f;

        [Header("시간 (초)")]
        [SerializeField] float spreadSeconds = 0.3f;
        [SerializeField] float stripSeconds = 0.35f;
        [SerializeField] float beamSeconds = 0.4f;
        [SerializeField] float gatherSeconds = 0.25f;

        [Header("빛줄기")]
        [SerializeField] float beamHeight = 0.09f;
        [Tooltip("특수 팩 빛줄기 높이 배율")]
        [SerializeField] float specialBeamScale = 1.8f;
        [SerializeField, Range(0f, 1f)] float beamAlpha = 0f; // 빛줄기 끔: 등급이 미리 드러나고 흰 빈 카드처럼 보임 (DEC-004 수정)
        [SerializeField, Range(0f, 1f)] float tearVolume = 0.5f;

        sealed class VisualPack
        {
            public Transform Root;
            public Transform Strip;
            public SpriteRenderer Beam;
        }

        readonly List<VisualPack> packs = new List<VisualPack>();
        Coroutine routine;

        public bool IsPlaying => routine != null;

        public static BoxOpenEffect Create(Transform parent, int prewarm)
        {
            var go = new GameObject("BoxOpenEffect");
            go.transform.SetParent(parent, false);
            var effect = go.AddComponent<BoxOpenEffect>();
            effect.EnsurePacks(prewarm);
            return effect;
        }

        /// <summary>
        /// 아치 위 index번째 팩 자리 (왼쪽 → 오른쪽). 앞면(-Z)이 머리 쪽을 봅니다.
        /// </summary>
        /// <param name="forward">플레이어 정면 (수평으로 맞춤)</param>
        public static Pose ArchPose(int index, int count, Vector3 head, Vector3 forward, float radius, float halfAngle, float drop, float lift)
        {
            Vector3 f = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
            f.Normalize();
            float u = count <= 1 ? 0f : index / (float)(count - 1) * 2f - 1f; // -1(왼쪽) ~ 1(오른쪽)
            Vector3 dir = Quaternion.AngleAxis(u * halfAngle, Vector3.up) * f;
            Vector3 pos = head + dir * radius + Vector3.down * drop + Vector3.up * (lift * (1f - u * u));
            return new Pose(pos, Quaternion.LookRotation(dir, Vector3.up));
        }

        /// <summary>연출을 시작하고 전체 시간(초)을 돌려줍니다. 끝나면 카드 묶음을 보여 주면 됩니다.</summary>
        /// <param name="gather">팩들이 나왔다가 다시 모이는 자리 (카드 묶음 자리)</param>
        /// <param name="interval">팩마다 뜯기는 간격 (초)</param>
        /// <param name="beamColor">팩별 빛줄기 색 (특수 팩이면 금빛을 주면 됨)</param>
        public float Play(IReadOnlyList<PackResult> results, Vector3 head, Vector3 forward, Pose gather, float interval,
            Func<PackResult, Color> beamColor, AudioClip tearSound, AudioClip specialSound)
        {
            Stop();
            int n = results.Count;
            EnsurePacks(n);
            float total = spreadSeconds + Mathf.Max(0, n - 1) * interval + Mathf.Max(stripSeconds, beamSeconds) + gatherSeconds;
            routine = StartCoroutine(Run(results, head, forward, gather, interval, beamColor, tearSound, specialSound));
            return total;
        }

        public void Stop()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            foreach (VisualPack p in packs) p.Root.gameObject.SetActive(false);
        }

        IEnumerator Run(IReadOnlyList<PackResult> results, Vector3 head, Vector3 forward, Pose gather, float interval,
            Func<PackResult, Color> beamColor, AudioClip tearSound, AudioClip specialSound)
        {
            int n = results.Count;
            var homes = new Pose[n];
            var colors = new Color[n];
            var special = new bool[n];
            var torn = new bool[n];
            for (int i = 0; i < n; i++)
            {
                homes[i] = ArchPose(i, n, head, forward, archRadius, archHalfAngle, archDrop, archLift);
                special[i] = false; // 뜯는 순간 등급·특수 팩이 미리 드러나지 않게 (DEC-004)
                colors[i] = beamColor != null ? beamColor(results[i]) : Color.white;
                ResetPack(packs[i]);
            }

            float tearStart = spreadSeconds;
            float gatherStart = tearStart + Mathf.Max(0, n - 1) * interval + Mathf.Max(stripSeconds, beamSeconds);
            float end = gatherStart + gatherSeconds;
            for (float time = 0f; time < end; time += Time.deltaTime)
            {
                for (int i = 0; i < n; i++)
                {
                    VisualPack vp = packs[i];

                    // 위치: 묶음 자리 → 아치 (빠르게 나갔다가 느려짐) … 아치 → 묶음 자리 (점점 빨라지며 작아짐)
                    Vector3 pos;
                    Quaternion rot;
                    float scale;
                    if (time < gatherStart)
                    {
                        float t = Mathf.Clamp01((time - i * 0.015f) / spreadSeconds);
                        float e = 1f - (1f - t) * (1f - t);
                        pos = Vector3.Lerp(gather.position, homes[i].position, e);
                        rot = Quaternion.Slerp(gather.rotation, homes[i].rotation, e);
                        scale = Mathf.Lerp(0.3f, 1f, e);
                    }
                    else
                    {
                        float t = Mathf.Clamp01((time - gatherStart) / gatherSeconds);
                        float e = t * t;
                        pos = Vector3.Lerp(homes[i].position, gather.position, e);
                        rot = Quaternion.Slerp(homes[i].rotation, gather.rotation, e);
                        scale = 1f - e;
                    }
                    vp.Root.SetPositionAndRotation(pos, rot);
                    vp.Root.localScale = Vector3.one * (packScale * Mathf.Max(0.001f, scale));

                    // 뜯김: 윗띠가 위로 튀었다가 옆으로 떨어지며 작아짐 + 틈에서 빛줄기
                    float tt = time - (tearStart + i * interval);
                    if (tt < 0f) continue;
                    if (!torn[i])
                    {
                        torn[i] = true;
                        AudioClip clip = special[i] && specialSound != null ? specialSound : tearSound;
                        if (clip != null) AudioSource.PlayClipAtPoint(clip, pos, tearVolume);
                    }

                    float s = Mathf.Clamp01(tt / stripSeconds);
                    vp.Strip.localPosition = new Vector3(0.035f * s, H * 0.5f - SealH * 0.5f + 0.04f * 4f * s * (1f - s) - 0.04f * s * s, -0.0004f);
                    vp.Strip.localRotation = Quaternion.Euler(0f, 0f, -120f * s);
                    vp.Strip.localScale = Vector3.one * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, s)));

                    float b = Mathf.Clamp01(tt / beamSeconds);
                    Color c = colors[i];
                    c.a = beamAlpha * Mathf.Sin(b * Mathf.PI);
                    vp.Beam.color = c;
                    float height = beamHeight * (special[i] ? specialBeamScale : 1f);
                    vp.Beam.transform.localScale = new Vector3(W * (special[i] ? 1.1f : 0.9f), height * Mathf.Lerp(0.3f, 1f, 1f - (1f - b) * (1f - b)), 1f);
                }
                yield return null;
            }

            for (int i = 0; i < n; i++) packs[i].Root.gameObject.SetActive(false);
            routine = null;
        }

        // (MonoBehaviour의 Reset 메시지와 이름이 겹치지 않게 ResetPack)
        void ResetPack(VisualPack vp)
        {
            vp.Root.gameObject.SetActive(true);
            vp.Strip.localPosition = new Vector3(0f, H * 0.5f - SealH * 0.5f, -0.0004f);
            vp.Strip.localRotation = Quaternion.identity;
            vp.Strip.localScale = Vector3.one;
            vp.Beam.color = Color.clear;
            vp.Beam.transform.localScale = new Vector3(W * 0.9f, beamHeight * 0.3f, 1f);
        }

        void EnsurePacks(int count)
        {
            while (packs.Count < count) packs.Add(BuildPack(packs.Count));
        }

        // 시각용 팩: 앞면 · 뒷면 · 아래 띠 · 윗띠(뜯겨 나갈 조각) · 빛줄기. 판 5장.
        VisualPack BuildPack(int index)
        {
            var root = new GameObject($"BoxPack{index}").transform;
            root.SetParent(transform, false);
            float bodyH = H - 2f * SealH;
            RevealSprites.Quad(root, "Front", RevealSprites.Center, new Vector3(0f, 0f, -0.0004f), new Vector2(W, bodyH), FrontColor, 0);
            RevealSprites.Quad(root, "Back", RevealSprites.Center, new Vector3(0f, 0f, 0.0004f), new Vector2(W, bodyH), BackColor, 0);
            RevealSprites.Quad(root, "SealBottom", RevealSprites.Center, new Vector3(0f, -(H * 0.5f - SealH * 0.5f), 0f), new Vector2(W, SealH), SealColor, 0);

            var strip = new GameObject("TornStrip").transform;
            strip.SetParent(root, false);
            RevealSprites.Quad(strip, "SealTop", RevealSprites.Center, Vector3.zero, new Vector2(W, SealH), SealColor, 0);

            SpriteRenderer beam = RevealSprites.Quad(root, "Beam", RevealSprites.Bottom, new Vector3(0f, H * 0.5f - SealH, 0f),
                new Vector2(W * 0.9f, beamHeight * 0.3f), Color.clear, 1);

            root.gameObject.SetActive(false);
            return new VisualPack { Root = root, Strip = strip, Beam = beam };
        }
    }
}
