using System.Collections.Generic;
using CardShop.UI;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 대상 강조 (빌드 피드백 11-19 2차: 물건만 은은하게). 물건 모양 그대로의 분홍 외곽선(OutlineMarker)만 씁니다.
    /// 예전의 선 상자 · 바닥 고리 · 무대 조명 원뿔은 몰입을 깨서 뺐습니다.
    /// 외곽선을 못 붙이는 대상(아주 얇은 카드 · 글자 · 정적 배칭 물건)만 대상 위에 작은 "∨" 표시를 띄웁니다.
    /// 외곽선 밝기는 느리게 숨 쉬듯 바뀌고, 엉뚱한 물건을 잡으면 잠깐 진해집니다(Flash).
    /// 대상은 손에 들려 움직이므로 표시 자리는 매 프레임 다시 잽니다. 꺼지거나 사라진 대상은 그리지 않습니다.
    /// </summary>
    public sealed class TargetHighlighter : MonoBehaviour
    {
        const int MaxTargets = 4;
        const float FlashSeconds = 0.6f;

        [Tooltip("숨 쉬기 속도 (회/초)")]
        [SerializeField] float pulseSpeed = 0.5f;
        [Tooltip("외곽선 · 표시 색 (분홍 = 튜토리얼 대상, UiTheme.Accent)")]
        [SerializeField] Color color = UiTheme.Accent;
        [Tooltip("외곽선 두께 (m)")]
        [SerializeField] float outlineWidth = 0.004f;
        [Tooltip("외곽선을 못 붙인 대상 위 ∨ 표시: 대상 윗면에서 띄우는 높이 (m)")]
        [SerializeField] float markerGap = 0.06f;
        [Tooltip("∨ 표시 크기 (m)")]
        [SerializeField] float markerSize = 0.035f;
        [Tooltip("∨ 표시 선 굵기 (m)")]
        [SerializeField] float markerWidth = 0.006f;

        readonly List<HighlightTarget> targets = new List<HighlightTarget>(MaxTargets);
        readonly LineRenderer[] markers = new LineRenderer[MaxTargets];
        readonly bool[] outlined = new bool[MaxTargets];
        readonly Vector3[] markerPoints = new Vector3[3];
        Material material;
        OutlineMarker outline;
        float flashLeft;

        void Awake()
        {
            material = CreateMaterial();
            for (int i = 0; i < MaxTargets; i++) markers[i] = CreateLine("Marker" + i);
            outline = new OutlineMarker();
        }

        void OnDestroy()
        {
            outline?.Dispose();
            if (material != null) Destroy(material);
        }

        /// <summary>대상 목록을 복사해 강조를 켭니다 (앞에서 최대 4개).</summary>
        public void Show(IReadOnlyList<HighlightTarget> list)
        {
            targets.Clear();
            outline.Clear();
            if (list != null)
                for (int i = 0; i < list.Count && targets.Count < MaxTargets; i++)
                    if (list[i] != null) targets.Add(list[i]);
            for (int i = 0; i < targets.Count; i++) outlined[i] = outline.Mark(targets[i]);
            enabled = true;
            if (targets.Count == 0) HideMarkers();
        }

        public void Hide()
        {
            targets.Clear();
            outline.Clear();
            flashLeft = 0f;
            HideMarkers();
        }

        /// <summary>잠깐 더 진하게 깜빡입니다 (다른 물건을 잡았을 때).</summary>
        public void Flash() => flashLeft = FlashSeconds;

        void LateUpdate()
        {
            flashLeft = Mathf.Max(0f, flashLeft - Time.unscaledDeltaTime);
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f);
            float flash = flashLeft / FlashSeconds; // 1 → 0
            Color glow = Color.Lerp(color, Color.white, 0.25f * wave + 0.5f * flash);
            outline.Paint(glow, outlineWidth * (1f + 0.25f * wave + flash));

            Transform head = Camera.main != null ? Camera.main.transform : null;
            for (int i = 0; i < MaxTargets; i++)
            {
                HighlightTarget t = i < targets.Count ? targets[i] : null;
                bool show = t != null && !outlined[i] && head != null && t.TryGetBounds(out Bounds b) && Place(markers[i], b, head, wave);
                if (markers[i].enabled != show) markers[i].enabled = show;
                if (show) Paint(markers[i], glow, markerWidth * (1f + flash));
            }
        }

        // 대상 윗면 위에 아래를 가리키는 ∨. 위아래로 살짝 떠다니고, 늘 플레이어 쪽을 봄
        bool Place(LineRenderer line, Bounds b, Transform head, float wave)
        {
            Vector3 tip = new Vector3(b.center.x, b.max.y + markerGap + 0.015f * wave, b.center.z);
            Vector3 toHead = head.position - tip;
            toHead.y = 0f;
            if (toHead.sqrMagnitude < 0.0001f) return false;
            Vector3 side = Vector3.Cross(Vector3.up, toHead.normalized) * markerSize;
            Vector3 up = Vector3.up * markerSize;
            markerPoints[0] = tip + up - side;
            markerPoints[1] = tip;
            markerPoints[2] = tip + up + side;
            line.SetPositions(markerPoints);
            return true;
        }

        static void Paint(LineRenderer line, Color c, float width)
        {
            line.startColor = c;
            line.endColor = c;
            line.widthMultiplier = width;
        }

        void HideMarkers()
        {
            for (int i = 0; i < MaxTargets; i++)
                if (markers[i] != null) markers[i].enabled = false;
        }

        LineRenderer CreateLine(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = markerPoints.Length;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = material;
            line.enabled = false;
            return line;
        }

        // Sprites/Default는 GraphicsSettings의 항상 포함 셰이더. 없으면 SpriteRenderer 기본 머티리얼을 복사
        static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) return new Material(shader) { name = "TutorialMarker" };
            var temp = new GameObject("TempSprite");
            var sr = temp.AddComponent<SpriteRenderer>();
            var mat = new Material(sr.sharedMaterial) { name = "TutorialMarker" };
            Destroy(temp);
            return mat;
        }
    }
}
