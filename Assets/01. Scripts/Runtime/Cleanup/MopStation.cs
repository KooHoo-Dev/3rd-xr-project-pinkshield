using System.Collections;
using CardShop.Customers;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 빗자루 → 대걸레 (빌드피드백_11-19_FIX설계 F4, DEC-030). 씬을 다시 빌드하지 않게, 씬이 열리면 빗자루(Broom)에 스스로 붙어
    /// ① 솔 모양을 숨기고 대걸레 머리(걸레 줄)를 붙이고 ② 발밑에 양동이 거치대를 두고 ③ 놓으면 1초 뒤 양동이로 돌아가게 합니다.
    /// 도둑 맞히기(Broom: 머리 위치 · 휘두르는 속도)는 그대로입니다. 얼룩을 문지르면(StainSpot → Scrub) 걸레 줄이 기울고 손이 떨립니다.
    /// </summary>
    [RequireComponent(typeof(Broom), typeof(HeldItem))]
    public sealed class MopStation : MonoBehaviour
    {
        const float ReturnDelay = 1f;
        const float BrushMaxY = 0.2f;              // 이 높이 아래 빗자루 모양(솔 · 솔 받침)은 숨김
        const int StrandCount = 10;
        static readonly Color StrandColor = new Color(0.82f, 0.86f, 0.9f);
        static readonly Color CollarColor = new Color(0.3f, 0.55f, 0.85f);
        static readonly Color BucketColor = new Color(0.95f, 0.75f, 0.2f);

        const float TiltAmount = 0.45f;            // 걸레 줄이 끌리는 쪽으로 기우는 정도
        const float TiltSharpness = 12f;

        HeldItem held;
        XRGrabInteractable grab;
        Pose home;
        Coroutine returning;
        float scrubUntil;
        Vector3 dragDir;

        /// <summary>걸레 줄 묶음 (F5에서 바닥에 닿으면 기울임)</summary>
        public Transform Strands { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            Broom broom = FindAnyObjectByType<Broom>(FindObjectsInactive.Include);
            if (broom != null && broom.GetComponent<MopStation>() == null && broom.GetComponent<HeldItem>() != null)
                broom.gameObject.AddComponent<MopStation>();
        }

        void Awake()
        {
            held = GetComponent<HeldItem>();
            grab = GetComponent<XRGrabInteractable>();
            home = new Pose(transform.position, transform.rotation);
            Material baseMat = Restyle();
            BuildBucket(baseMat);
            RenameLabel();
        }

        void OnEnable() { held.Released += OnReleased; held.Grabbed += OnGrabbed; }
        void OnDisable() { held.Released -= OnReleased; held.Grabbed -= OnGrabbed; }

        void OnGrabbed(HeldItem _)
        {
            if (returning != null) StopCoroutine(returning);
            returning = null;
        }

        void OnReleased(HeldItem _)
        {
            if (returning != null) StopCoroutine(returning);
            returning = StartCoroutine(ReturnHome());
        }

        /// <summary>얼룩을 문지르는 중 (StainSpot이 매 프레임): 걸레 줄이 끌리는 반대쪽으로 기울고, 쥔 손이 속도만큼 떨림</summary>
        public void Scrub(Vector3 velocity)
        {
            scrubUntil = Time.time + 0.1f;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 1e-4f) dragDir = velocity.normalized;
            if (grab != null && grab.firstInteractorSelecting is XRBaseInputInteractor hand)
                hand.SendHapticImpulse(Mathf.Clamp(velocity.magnitude * 0.4f, 0.1f, 0.4f), 0.05f);
        }

        // 문지르는 동안 걸레 줄 끝이 움직이는 반대쪽으로 끌림, 멈추면 다시 곧게
        void LateUpdate()
        {
            if (Strands == null) return;
            Quaternion target = Quaternion.identity;
            if (Time.time < scrubUntil)
            {
                Vector3 hang = -transform.up;
                Quaternion world = Quaternion.FromToRotation(hang, (hang - dragDir * TiltAmount).normalized) * transform.rotation;
                target = Quaternion.Inverse(transform.rotation) * world;
            }
            Strands.localRotation = Quaternion.Slerp(Strands.localRotation, target, 1f - Mathf.Exp(-TiltSharpness * Time.deltaTime));
        }

        // 놓은 자리에 잠깐 섰다가(HeldItem) 양동이로: 바닥에 쓰러진 대걸레를 찾아다니지 않게 (F6 ③)
        IEnumerator ReturnHome()
        {
            yield return new WaitForSeconds(ReturnDelay);
            returning = null;
            if (!held.IsHeld) held.PlaceAt(home.position, home.rotation);
        }

        // 솔 모양을 숨기고 대걸레 머리를 붙임. 솔 부분 콜라이더(BoxCollider)는 대걸레 머리 크기로. 막대 재질을 복제해 색만 바꿔 씀
        Material Restyle()
        {
            Material baseMat = null;
            foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (transform.InverseTransformPoint(r.bounds.center).y < BrushMaxY) r.enabled = false;
                else if (baseMat == null) baseMat = r.sharedMaterial;
            }
            if (GetComponent<BoxCollider>() is BoxCollider brush)
            {
                brush.center = new Vector3(0f, 0.08f, 0f);
                brush.size = new Vector3(0.2f, 0.16f, 0.2f);
            }

            Part(transform, PrimitiveType.Cylinder, new Vector3(0.06f, 0.03f, 0.06f), new Vector3(0f, 0.17f, 0f), Tint(baseMat, CollarColor));
            Strands = new GameObject("MopStrands").transform;
            Strands.SetParent(transform, false);
            Strands.localPosition = new Vector3(0f, 0.16f, 0f); // 걸레 줄이 매달리는 점 (F5에서 이 점을 기준으로 기울임)
            Material strand = Tint(baseMat, StrandColor);
            for (int i = 0; i < StrandCount; i++)
            {
                float a = i * Mathf.PI * 2f / StrandCount;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Transform s = Part(Strands, PrimitiveType.Cube, new Vector3(0.025f, 0.15f, 0.025f), dir * 0.035f + Vector3.down * 0.075f, strand);
                s.localRotation = Quaternion.AngleAxis(12f, Vector3.Cross(Vector3.up, dir)); // 끝이 바깥으로 벌어짐
            }
            return baseMat;
        }

        // 대걸레가 서 있는 양동이 (부모는 대걸레가 아니라 같은 부모: 대걸레를 들어도 양동이는 남음)
        void BuildBucket(Material baseMat)
        {
            Transform bucket = Part(transform.parent, PrimitiveType.Cylinder, new Vector3(0.3f, 0.14f, 0.3f), Vector3.zero, Tint(baseMat, BucketColor));
            bucket.name = "MopBucket";
            bucket.position = home.position + Vector3.up * 0.14f;
        }

        static void RenameLabel()
        {
            foreach (TextMeshPro t in FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.text.StartsWith("빗자루")) t.text = "대걸레\n(청소 · 도둑 잡기)";
        }

        static Transform Part(Transform parent, PrimitiveType shape, Vector3 size, Vector3 localPos, Material mat) =>
            Core.RuntimeShapes.Create(shape, shape.ToString(), parent, localPos, size, mat, castShadows: false).transform;

        static Material Tint(Material source, Color color) =>
            source != null ? new Material(source) { color = color } : null;
    }
}
