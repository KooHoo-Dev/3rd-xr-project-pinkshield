using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 대상을 감싸는 빛나는 선 상자 + 바닥 고리 (포스트 프로세싱 없이).
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
        [SerializeField] float pulseSpeed = 1.2f;
        [Tooltip("선 색 (주황)")]
        [SerializeField] Color color = new Color(1f, 0.6f, 0.15f, 1f);
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
        Material material;
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
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        /// <summary>대상 목록을 복사해 강조를 켭니다 (앞에서 최대 4개).</summary>
        public void Show(IReadOnlyList<HighlightTarget> list)
        {
            targets.Clear();
            if (list != null)
                for (int i = 0; i < list.Count && targets.Count < MaxTargets; i++)
                    if (list[i] != null) targets.Add(list[i]);
            enabled = true;
            if (targets.Count == 0) HideLines();
        }

        public void Hide()
        {
            targets.Clear();
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

            for (int i = 0; i < MaxTargets; i++)
            {
                HighlightTarget t = i < targets.Count ? targets[i] : null;
                if (t == null || !t.TryGetBounds(out Bounds b))
                {
                    SetShown(i, false);
                    continue;
                }
                b.Expand(padding * 2f);
                DrawBox(boxes[i], b);
                DrawRing(rings[i], b);
                Paint(boxes[i], c, width);
                Paint(rings[i], c, width);
                SetShown(i, true);
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

        void SetShown(int i, bool on)
        {
            if (boxes[i].enabled != on) boxes[i].enabled = on;
            if (rings[i].enabled != on) rings[i].enabled = on;
        }

        void HideLines()
        {
            for (int i = 0; i < MaxTargets; i++) SetShown(i, false);
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
