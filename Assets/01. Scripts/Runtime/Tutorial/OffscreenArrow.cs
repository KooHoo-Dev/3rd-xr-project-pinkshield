using System;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 시야 밖 대상 쪽을 가리키는 작은 화살표. 대상이 머리 정면에서 <see cref="offscreenAngle"/>도를 넘으면
    /// 머리 앞 시야 가장자리 쪽에 띄우고, 대상이 시야 안으로 들어오면 숨깁니다.
    /// 머리에 딱 붙이지 않고 부드럽게 따라가게 해서 멀미를 줄입니다.
    /// 모양(삼각형 + 기둥)은 코드로 만든 메시 하나, 머티리얼은 Sprites/Default (항상 포함 셰이더).
    /// </summary>
    public sealed class OffscreenArrow : MonoBehaviour
    {
        const float HideHysteresis = 5f; // 들어올 때는 이만큼 더 안쪽이어야 숨김 (깜빡임 방지)

        [Tooltip("이 각도(머리 정면 기준)를 넘으면 화살표를 띄움")]
        [SerializeField] float offscreenAngle = 40f;
        [Tooltip("머리에서 화살표까지 (m)")]
        [SerializeField] float distance = 0.9f;
        [Tooltip("시야 가운데에서 대상 쪽으로 밀어내는 거리 (m)")]
        [SerializeField] float edgeOffset = 0.25f;
        [Tooltip("따라가는 빠르기 (클수록 머리에 붙은 느낌)")]
        [SerializeField] float followSharpness = 6f;
        [Tooltip("화살표 길이 (m)")]
        [SerializeField] float size = 0.08f;
        [SerializeField] Color color = new Color(1f, 0.6f, 0.15f, 0.95f);

        Func<Vector3?> target;
        MeshRenderer meshRenderer;
        Material material;
        Mesh mesh;
        bool shown;

        void Awake()
        {
            var go = new GameObject("Arrow");
            go.transform.SetParent(transform, false);
            mesh = BuildMesh(size);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = go.AddComponent<MeshRenderer>();
            material = CreateMaterial();
            material.color = color;
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (mesh != null) Destroy(mesh);
        }

        /// <summary>가리킬 대상. null이거나 null을 돌려주면 숨깁니다.</summary>
        public void SetTarget(Func<Vector3?> targetPosition)
        {
            target = targetPosition;
            if (target == null) SetShown(false);
        }

        void LateUpdate()
        {
            Vector3? point = target?.Invoke();
            Camera cam = Camera.main;
            if (!point.HasValue || cam == null) { SetShown(false); return; }

            Transform head = cam.transform;
            Vector3 toTarget = point.Value - head.position;
            float angle = Vector3.Angle(head.forward, toTarget);
            float limit = shown ? offscreenAngle - HideHysteresis : offscreenAngle;
            if (angle < limit) { SetShown(false); return; }

            // 시야 평면에서 대상 쪽 방향 (바로 뒤면 오른쪽)
            Vector3 local = head.InverseTransformDirection(toTarget);
            var flat = new Vector2(local.x, local.y);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector2.right;
            flat.Normalize();
            Vector3 pointDir = head.right * flat.x + head.up * flat.y;

            Vector3 pos = head.position + head.forward * distance + pointDir * edgeOffset;
            // 메시는 +Y로 가리키고, -Z 면이 머리 쪽
            Quaternion rot = Quaternion.LookRotation(head.forward, pointDir);

            if (!shown)
            {
                transform.SetPositionAndRotation(pos, rot);
                SetShown(true);
                return;
            }
            float k = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, pos, k),
                                             Quaternion.Slerp(transform.rotation, rot, k));
        }

        void SetShown(bool on)
        {
            shown = on;
            if (meshRenderer.enabled != on) meshRenderer.enabled = on;
        }

        // XY 평면의 화살표 (끝이 +Y). 양면은 셰이더(Cull Off)가 그려 줌
        static Mesh BuildMesh(float length)
        {
            float head = length * 0.5f, halfHead = length * 0.4f, halfStem = length * 0.13f;
            float tip = length * 0.5f, baseY = tip - head, tail = -length * 0.5f;
            var m = new Mesh { name = "TutorialArrow" };
            m.vertices = new[]
            {
                new Vector3(0f, tip, 0f), new Vector3(halfHead, baseY, 0f), new Vector3(-halfHead, baseY, 0f),
                new Vector3(-halfStem, baseY, 0f), new Vector3(halfStem, baseY, 0f),
                new Vector3(halfStem, tail, 0f), new Vector3(-halfStem, tail, 0f),
            };
            m.triangles = new[] { 0, 1, 2, 3, 4, 5, 3, 5, 6 };
            // Sprites/Default는 정점 색 × 머티리얼 색이라 정점 색을 흰색으로 채움
            var white = new Color32[7];
            for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
            m.colors32 = white;
            m.RecalculateBounds();
            return m;
        }

        // Sprites/Default는 GraphicsSettings의 항상 포함 셰이더. 없으면 SpriteRenderer 기본 머티리얼을 복사
        static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) return new Material(shader) { name = "TutorialArrow" };
            var temp = new GameObject("TempSprite");
            var sr = temp.AddComponent<SpriteRenderer>();
            var mat = new Material(sr.sharedMaterial) { name = "TutorialArrow" };
            Destroy(temp);
            return mat;
        }
    }
}
