using System.Collections.Generic;
using CardShop.UI;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 대상 강조 (포스트 프로세싱 없이, FIX C2): 물건 모양 그대로의 분홍 외곽선(OutlineMarker) + 바닥 고리,
    /// 정지 단계에서는 위에서 내려오는 가짜 무대 조명(TargetStageLight). 외곽선을 못 붙이는 얇은 물건 · UI는 예전 선 상자로 그립니다.
    /// 외곽선 두께 · 밝기와 선은 1초 주기로 커졌다 작아집니다.
    /// 대상은 손에 들려 움직이므로 bounds를 매 프레임 다시 잽니다. 꺼지거나 사라진 대상은 그리지 않습니다.
    /// 선은 LineRenderer, 머티리얼은 Awake에서 한 번만 만듭니다 (Sprites/Default: 항상 포함 셰이더라 Quest 빌드에서도 빠지지 않음).
    /// </summary>
    public sealed class TargetHighlighter : MonoBehaviour
    {
        const int MaxTargets = 4;
        const int RingPoints = 32;
        const float FlashSeconds = 0.6f;

        // 상자 12모서리를 한 줄로 잇는 꼭짓점 순서 (일부 모서리는 두 번 지남)
        static readonly int[] BoxPath = { 0, 1, 2, 3, 0, 4, 5, 6, 7, 4, 5, 1, 2, 6, 7, 3 };

        [Tooltip("대상 bounds 바깥으로 띄우는 여백 (m)")]
        [SerializeField] float padding = 0.03f;
        [Tooltip("깜빡임 속도 (회/초)")]
        [SerializeField] float pulseSpeed = 1f;
        [Tooltip("선 · 외곽선 색 (분홍 = 튜토리얼 대상, UiTheme.Accent)")]
        [SerializeField] Color color = UiTheme.Accent;
        [Tooltip("외곽선 두께 (m)")]
        [SerializeField] float outlineWidth = 0.006f;
        [Tooltip("선 굵기 (m)")]
        [SerializeField] float lineWidth = 0.008f;
        [Tooltip("깜빡일 때 가장 옅은 알파")]
        [SerializeField, Range(0f, 1f)] float minAlpha = 0.4f;

        readonly List<HighlightTarget> targets = new List<HighlightTarget>(MaxTargets);
        readonly LineRenderer[] boxes = new LineRenderer[MaxTargets];
        readonly LineRenderer[] rings = new LineRenderer[MaxTargets];
        readonly Vector3[] corners = new Vector3[8];
        readonly Vector3[] boxPoints = new Vector3[16];
        readonly Vector3[] ringPoints = new Vector3[RingPoints];
        readonly bool[] outlined = new bool[MaxTargets];
        Material material, coneMaterial;
        OutlineMarker outline;
        TargetStageLight stage;
        bool spotlight;
        float flashLeft;

        void Awake()
        {
            material = CreateMaterial();
            for (int i = 0; i < MaxTargets; i++)
            {
                boxes[i] = CreateLine("Box" + i, boxPoints.Length, false);
                rings[i] = CreateLine("Ring" + i, RingPoints, true);
                // 고리는 바닥에 눕힘 (선의 면 = 오브젝트 Z축 = 아래)
                rings[i].alignment = LineAlignment.TransformZ;
                rings[i].transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            outline = new OutlineMarker();
            coneMaterial = new Material(material) { name = "TutorialStageLight", color = Color.Lerp(color, Color.white, 0.6f) };
            stage = new TargetStageLight(transform, coneMaterial, MaxTargets);
        }

        void OnDestroy()
        {
            outline?.Dispose();
            if (material != null) Destroy(material);
            if (coneMaterial != null) Destroy(coneMaterial);
        }

        /// <summary>대상 목록을 복사해 강조를 켭니다 (앞에서 최대 4개). stageLight면 무대 조명도 (정지 단계)</summary>
        public void Show(IReadOnlyList<HighlightTarget> list, bool stageLight = false)
        {
            targets.Clear();
            outline.Clear();
            if (list != null)
                for (int i = 0; i < list.Count && targets.Count < MaxTargets; i++)
                    if (list[i] != null) targets.Add(list[i]);
            for (int i = 0; i < targets.Count; i++) outlined[i] = outline.Mark(targets[i]);
            spotlight = stageLight;
            enabled = true;
            if (targets.Count == 0) HideLines();
        }

        public void Hide()
        {
            targets.Clear();
            outline.Clear();
            flashLeft = 0f;
            HideLines();
        }

        /// <summary>잠깐 더 진하고 굵게 깜빡입니다 (다른 물건을 잡았을 때).</summary>
        public void Flash() => flashLeft = FlashSeconds;

        /// <summary>살아 있는 첫 대상의 중심. 없으면 false.</summary>
        public bool HasVisibleTarget(out Vector3 center)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                HighlightTarget t = targets[i];
                if (t != null && t.TryGetBounds(out Bounds b)) { center = b.center; return true; }
            }
            center = default;
            return false;
        }

        void LateUpdate()
        {
            flashLeft = Mathf.Max(0f, flashLeft - Time.unscaledDeltaTime);
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f);
            float flash = flashLeft / FlashSeconds; // 1 → 0
            Color c = color;
            c.a = Mathf.Max(Mathf.Lerp(minAlpha, 1f, wave), flash);
            float width = lineWidth * (1f + 0.3f * wave + 1.5f * flash);
            Color glow = Color.Lerp(color, Color.white, 0.35f * wave + 0.5f * flash);
            outline.Paint(glow, outlineWidth * (1f + 0.5f * wave + 1.5f * flash));

            for (int i = 0; i < MaxTargets; i++)
            {
                HighlightTarget t = i < targets.Count ? targets[i] : null;
                if (t == null || !t.TryGetBounds(out Bounds b))
                {
                    SetShown(i, false);
                    stage.Place(i, null);
                    continue;
                }
                stage.Place(i, spotlight && !InHand(t) ? b : (Bounds?)null); // 손에 든 물건 위 조명은 어색함
                b.Expand(padding * 2f);
                DrawBox(boxes[i], b);
                DrawRing(rings[i], b);
                Paint(boxes[i], c, width);
                Paint(rings[i], c, width);
                SetShown(i, true, !outlined[i]);
            }
        }

        void DrawBox(LineRenderer line, Bounds b)
        {
            Vector3 n = b.min, x = b.max;
            corners[0] = new Vector3(n.x, n.y, n.z);
            corners[1] = new Vector3(x.x, n.y, n.z);
            corners[2] = new Vector3(x.x, n.y, x.z);
            corners[3] = new Vector3(n.x, n.y, x.z);
            corners[4] = new Vector3(n.x, x.y, n.z);
            corners[5] = new Vector3(x.x, x.y, n.z);
            corners[6] = new Vector3(x.x, x.y, x.z);
            corners[7] = new Vector3(n.x, x.y, x.z);
            for (int i = 0; i < BoxPath.Length; i++) boxPoints[i] = corners[BoxPath[i]];
            line.SetPositions(boxPoints);
        }

        // 대상 바닥 높이에 수평 고리
        void DrawRing(LineRenderer line, Bounds b)
        {
            float radius = Mathf.Max(b.extents.x, b.extents.z) * 1.15f;
            float y = b.min.y + 0.005f;
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i * Mathf.PI * 2f / RingPoints;
                ringPoints[i] = new Vector3(b.center.x + Mathf.Cos(a) * radius, y, b.center.z + Mathf.Sin(a) * radius);
            }
            line.SetPositions(ringPoints);
        }

        static void Paint(LineRenderer line, Color c, float width)
        {
            line.startColor = c;
            line.endColor = c;
            line.widthMultiplier = width;
        }

        // box: 외곽선을 못 붙인 대상만 선 상자
        void SetShown(int i, bool on, bool box = true)
        {
            if (boxes[i].enabled != (on && box)) boxes[i].enabled = on && box;
            if (rings[i].enabled != on) rings[i].enabled = on;
        }

        void HideLines()
        {
            for (int i = 0; i < MaxTargets; i++) SetShown(i, false);
            stage?.HideAll();
        }

        static bool InHand(HighlightTarget t)
        {
            var grab = t.GetComponentInParent<XRBaseInteractable>();
            return grab != null && grab.isSelected;
        }

        LineRenderer CreateLine(string name, int count, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = count;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
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
            if (shader != null) return new Material(shader) { name = "TutorialHighlight" };
            var temp = new GameObject("TempSprite");
            var sr = temp.AddComponent<SpriteRenderer>();
            var mat = new Material(sr.sharedMaterial) { name = "TutorialHighlight" };
            Destroy(temp);
            return mat;
        }
    }
}
