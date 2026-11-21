using System.Collections;
using System.Collections.Generic;
using CardShop.Cards;
using TMPro;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 상자 10팩 일괄 개봉 연출 (시연 피드백 B3). 판정은 이미 끝났고 보여 주기만 합니다(건너뛰어도 데이터는 같음).
    /// ① 카드 묶음 자리에서 팩 10개가 눈앞에 일렬(모바일 카드 게임식)로 펼쳐짐 ② 자르는 선이 한 번 가로지르며 왼쪽부터 차례로 윗띠가 뜯겨 날아감
    ///    ③ 팩들이 카드 묶음 자리로 모이며 사라짐. 등급이 미리 드러나지 않게 빛줄기 · 특수 팩 예고는 없습니다 (DEC-004 수정).
    /// 실제 팩 프리팹(Rigidbody · XRI · TMP)이 아니라 판(스프라이트) 4장짜리 시각용 팩 10개를 재사용합니다(Quest 성능).
    /// 단일 팩 뜯김(PackTearEffect)의 움직임을 줄여 같은 느낌으로 맞췄습니다.
    /// </summary>
    public sealed class BoxOpenEffect : MonoBehaviour
    {
        // 팩 프리팹(PackPrefabBuilder)과 같은 크기 · 색
        const float W = 0.075f, H = 0.13f, SealH = 0.014f;
        static readonly Color FrontColor = new Color(1f, 0.55f, 0.72f);
        static readonly Color BackColor = new Color(0.36f, 0.27f, 0.55f);
        static readonly Color SealColor = new Color(0.78f, 0.74f, 0.9f);

        [Header("일렬 배치 (모바일 카드 게임식: 한 줄로 늘어놓고 한 번에 가로질러 자름)")]
        [Tooltip("머리에서 팩 줄까지 거리 (m)")]
        [SerializeField] float rowDistance = 0.62f;
        [SerializeField] float rowGap = 0.012f;
        [Tooltip("눈높이보다 아래로 (m)")]
        [SerializeField] float rowDrop = 0.1f;
        [Tooltip("일렬 팩 크기 배율. 10팩이 한 시야에 들어오게 1보다 작게")]
        [SerializeField] float rowPackScale = 0.85f;
        [Tooltip("자르는 선이 줄 끝에서 끝까지 가로지르는 시간 (초)")]
        [SerializeField] float sweepSeconds = 0.3f;
        [Tooltip("팩이 펼쳐진 뒤 자르기 전 잠깐 멈춤 (초)")]
        [SerializeField] float sweepHold = 0.15f;

        [Header("시간 (초)")]
        [SerializeField] float spreadSeconds = 0.3f;
        [SerializeField] float stripSeconds = 0.35f;
        [Tooltip("마지막 팩이 뜯긴 뒤 모이기 시작할 때까지 (초)")]
        [SerializeField] float tearSettleSeconds = 0.4f;
        [SerializeField] float gatherSeconds = 0.25f;
        [SerializeField, Range(0f, 1f)] float tearVolume = 0.5f;

        sealed class VisualPack
        {
            public Transform Root;
            public Transform Strip;
        }

        readonly List<VisualPack> packs = new List<VisualPack>();
        static readonly Vector2 SlashSize = new Vector2(0.05f, 0.006f);

        SpriteRenderer slash;
        Coroutine routine;
        TMP_FontAsset font; // 팩 앞면 "SLIME PACK" 글자 (단일 팩과 같게, FIX D2)

        /// <summary>i번째 팩의 윗띠가 뜯긴 순간 (팩 번호, 그 팩의 위치 · 회전). 등급 이펙트용 (FIX D2 BoxPackRarityFx)</summary>
        public event System.Action<int, Pose> PackTorn;

        public static BoxOpenEffect Create(Transform parent, int prewarm, TMP_FontAsset font = null)
        {
            var go = new GameObject("BoxOpenEffect");
            go.transform.SetParent(parent, false);
            var effect = go.AddComponent<BoxOpenEffect>();
            effect.font = font;
            effect.EnsurePacks(prewarm);
            return effect;
        }

        /// <summary>일렬 배치의 index번째 팩 자리 (왼쪽 → 오른쪽). 앞면(-Z)이 머리 쪽을 봅니다.</summary>
        /// <param name="step">팩 중심 사이 간격 (m)</param>
        public static Pose RowPose(int index, int count, Vector3 head, Vector3 forward, float distance, float step, float drop)
        {
            Vector3 f = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
            f.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, f);
            float offset = (index - (count - 1) * 0.5f) * step;
            return new Pose(head + f * distance + Vector3.down * drop + right * offset, Quaternion.LookRotation(f, Vector3.up));
        }

        // 팩마다 뜯기는 간격: 자르는 선이 한 번 가로지르는 시간(sweepSeconds)에 맞춥니다.
        float TearInterval(int n) => sweepSeconds / Mathf.Max(1, n - 1);
        float TearStart => spreadSeconds + sweepHold;
        float GatherStart(int n) => TearStart + Mathf.Max(0, n - 1) * TearInterval(n) + Mathf.Max(stripSeconds, tearSettleSeconds);

        /// <summary>연출을 시작하고 전체 시간(초)을 돌려줍니다. 끝나면 카드 묶음을 보여 주면 됩니다.</summary>
        /// <param name="gather">팩들이 나왔다가 다시 모이는 자리 (카드 묶음 자리)</param>
        public float Play(int packCount, Vector3 head, Vector3 forward, Pose gather, AudioClip tearSound)
        {
            Stop();
            EnsurePacks(packCount);
            routine = StartCoroutine(Run(packCount, head, forward, gather, tearSound));
            return GatherStart(packCount) + gatherSeconds;
        }

        public void Stop()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            foreach (VisualPack p in packs) p.Root.gameObject.SetActive(false);
            if (slash != null) slash.color = Color.clear;
        }

        IEnumerator Run(int n, Vector3 head, Vector3 forward, Pose gather, AudioClip tearSound)
        {
            var homes = new Pose[n];
            var torn = new bool[n];
            for (int i = 0; i < n; i++)
            {
                homes[i] = RowPose(i, n, head, forward, rowDistance, (W + rowGap) * rowPackScale, rowDrop);
                ResetPack(packs[i]);
            }

            float interval = TearInterval(n);
            float tearStart = TearStart;
            float ps = rowPackScale;
            SpriteRenderer cut = EnsureSlash();
            float gatherStart = GatherStart(n);
            float end = gatherStart + gatherSeconds;
            for (float time = 0f; time < end; time += Time.deltaTime)
            {
                for (int i = 0; i < n; i++)
                {
                    VisualPack vp = packs[i];

                    // 위치: 묶음 자리 → 줄 (빠르게 나갔다가 느려짐) … 줄 → 묶음 자리 (점점 빨라지며 작아짐)
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
                    vp.Root.localScale = Vector3.one * (ps * Mathf.Max(0.001f, scale));

                    // 뜯김: 윗띠가 위로 튀었다가 옆으로 떨어지며 작아짐
                    float tt = time - (tearStart + i * interval);
                    if (tt < 0f) continue;
                    if (!torn[i])
                    {
                        torn[i] = true;
                        PackTorn?.Invoke(i, new Pose(pos, rot));
                        if (tearSound != null) AudioSource.PlayClipAtPoint(tearSound, pos, tearVolume);
                        else Sfx.Play(SoundId.PackTear, pos); // 인스펙터 칸이 비면 SoundBank
                    }

                    float s = Mathf.Clamp01(tt / stripSeconds);
                    vp.Strip.localPosition = new Vector3(0.035f * s, H * 0.5f - SealH * 0.5f + 0.04f * 4f * s * (1f - s) - 0.04f * s * s, -0.0004f);
                    vp.Strip.localRotation = Quaternion.Euler(0f, 0f, -120f * s);
                    vp.Strip.localScale = Vector3.one * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, s)));
                }
                UpdateSlash(cut, homes, ps, time - tearStart);
                yield return null;
            }

            if (slash != null) slash.color = Color.clear;
            for (int i = 0; i < n; i++) packs[i].Root.gameObject.SetActive(false);
            routine = null;
        }

        // 자르는 선: 윗띠 아래(팩이 열리는 줄)를 따라 왼쪽 끝에서 오른쪽 끝으로 한 번 가로지르는 밝은 막대
        void UpdateSlash(SpriteRenderer cut, Pose[] homes, float ps, float sweepTime)
        {
            if (sweepTime < 0f || sweepTime > sweepSeconds + 0.1f) { cut.color = Color.clear; return; }
            float t = Mathf.Clamp01(sweepTime / Mathf.Max(0.01f, sweepSeconds));
            Vector3 lift = homes[0].rotation * Vector3.up * ((H * 0.5f - SealH) * ps);
            Vector3 right = homes[0].rotation * Vector3.right;
            float half = (W + rowGap) * ps * 0.5f;
            Vector3 from = homes[0].position - right * half + lift;
            Vector3 to = homes[homes.Length - 1].position + right * half + lift;
            cut.transform.SetPositionAndRotation(Vector3.Lerp(from, to, t), homes[0].rotation);
            // 판 크기(SlashSize)에 팩 배율을 곱함. 배율만 넣으면 1m 판이 그대로 커져 흰 사각형이 눈앞을 가림 (Play 확인 A10)
            cut.transform.localScale = new Vector3(SlashSize.x * ps, SlashSize.y * ps, 1f);
            cut.color = new Color(1f, 1f, 1f, 0.95f * Mathf.Sin(t * Mathf.PI));
        }

        SpriteRenderer EnsureSlash()
        {
            if (slash == null)
                slash = RevealSprites.Quad(transform, "Slash", RevealSprites.Center, Vector3.zero, SlashSize, Color.clear, 3);
            return slash;
        }

        // (MonoBehaviour의 Reset 메시지와 이름이 겹치지 않게 ResetPack)
        void ResetPack(VisualPack vp)
        {
            vp.Root.gameObject.SetActive(true);
            vp.Strip.localPosition = new Vector3(0f, H * 0.5f - SealH * 0.5f, -0.0004f);
            vp.Strip.localRotation = Quaternion.identity;
            vp.Strip.localScale = Vector3.one;
        }

        void EnsurePacks(int count)
        {
            while (packs.Count < count) packs.Add(BuildPack(packs.Count));
        }

        static Sprite frontArt, backArt;
        static bool artSearched;

        // 가게 선반의 팩 프리팹 재질(PackFront · PackBackArt)에서 그림을 빌림 — 그림 파일 경로를 코드에 두지 않음
        static void FindArt()
        {
            if (artSearched) return;
            artSearched = true;
            var shelf = FindAnyObjectByType<CardShop.Interaction.ShelfController>(FindObjectsInactive.Include);
            if (shelf == null || shelf.PackPrefab == null) return;
            foreach (Renderer r in shelf.PackPrefab.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null || m.mainTexture is not Texture2D tex) continue;
                    if (frontArt == null && m.name.StartsWith("PackFront")) frontArt = ArtSprite(tex);
                    else if (backArt == null && m.name.StartsWith("PackBackArt")) backArt = ArtSprite(tex);
                }
        }

        // 가로 1 단위짜리 스프라이트 (세로 = 그림 비율). 판 크기는 ArtQuad가 배율로 맞춤
        static Sprite ArtSprite(Texture2D tex) =>
            Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);

        static void ArtQuad(Transform root, string name, Sprite art, Vector3 localPos, Vector2 size, bool back)
        {
            float aspect = art.rect.height / art.rect.width;
            SpriteRenderer sr = RevealSprites.Quad(root, name, art, localPos, new Vector2(size.x, size.y / aspect), Color.white, 0);
            sr.flipX = back; // 뒤에서 볼 때 글자가 거울처럼 뒤집히지 않게
        }

        // 단일 팩 프리팹(PackPrefabBuilder.Label)과 같은 앞면 글자
        void Label(Transform root)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var text = go.AddComponent<TextMeshPro>();
            if (font != null) text.font = font;
            text.text = "SLIME\nPACK";
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 0.22f;
            text.color = Color.white;
            text.GetComponent<MeshRenderer>().sortingOrder = 1; // 앞면 판(0) 위에
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(W * 0.9f, 0.05f);
            rt.localPosition = new Vector3(0f, -0.01f, -0.0008f);
        }

        // 시각용 팩: 앞면 · 뒷면 · 아래 띠 · 윗띠(뜯겨 나갈 조각) + 앞면 글자.
        VisualPack BuildPack(int index)
        {
            var root = new GameObject($"BoxPack{index}").transform;
            root.SetParent(transform, false);
            float bodyH = H - 2f * SealH;
            // 11-21 2차 B2: 팩 프리팹과 같은 MALLANG 앞 · 뒷면 그림. 그림을 못 구하면 예전 색 판 + "SLIME PACK" 글자
            FindArt();
            if (frontArt != null) ArtQuad(root, "Front", frontArt, new Vector3(0f, 0f, -0.0004f), new Vector2(W, bodyH), false);
            else RevealSprites.Quad(root, "Front", RevealSprites.Center, new Vector3(0f, 0f, -0.0004f), new Vector2(W, bodyH), FrontColor, 0);
            if (backArt != null) ArtQuad(root, "Back", backArt, new Vector3(0f, 0f, 0.0004f), new Vector2(W, bodyH), true);
            else RevealSprites.Quad(root, "Back", RevealSprites.Center, new Vector3(0f, 0f, 0.0004f), new Vector2(W, bodyH), BackColor, 0);
            RevealSprites.Quad(root, "SealBottom", RevealSprites.Center, new Vector3(0f, -(H * 0.5f - SealH * 0.5f), 0f), new Vector2(W, SealH), SealColor, 0);
            if (frontArt == null) Label(root);

            var strip = new GameObject("TornStrip").transform;
            strip.SetParent(root, false);
            RevealSprites.Quad(strip, "SealTop", RevealSprites.Center, Vector3.zero, new Vector2(W, SealH), SealColor, 0);

            root.gameObject.SetActive(false);
            return new VisualPack { Root = root, Strip = strip };
        }
    }
}
