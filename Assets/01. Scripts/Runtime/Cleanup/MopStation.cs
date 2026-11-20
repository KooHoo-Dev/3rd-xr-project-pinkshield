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
    /// ① 발밑에 양동이 거치대를 두고 ② 놓으면 1초 뒤 양동이로 돌아가게 합니다.
    /// 모양은 빗자루 그대로입니다 (11-20 사용자 결정: 걸레 줄 모양이 화장실 솔처럼 보여 되돌림, 이름만 "대걸레").
    /// 도둑 맞히기(Broom: 머리 위치 · 휘두르는 속도)는 그대로입니다. 얼룩을 문지르면(StainSpot → Scrub) 손이 떨립니다.
    /// </summary>
    [RequireComponent(typeof(Broom), typeof(HeldItem))]
    public sealed class MopStation : MonoBehaviour
    {
        const float ReturnDelay = 1f;
        static readonly Color BucketColor = new Color(0.95f, 0.75f, 0.2f);

        HeldItem held;
        XRGrabInteractable grab;
        Pose home;
        Coroutine returning;

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
            BuildBucket(GetComponentInChildren<MeshRenderer>(true)?.sharedMaterial);
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

        /// <summary>얼룩을 문지르는 중 (StainSpot이 매 프레임): 쥔 손이 속도만큼 떨림</summary>
        public void Scrub(Vector3 velocity)
        {
            velocity.y = 0f;
            if (grab != null && grab.firstInteractorSelecting is XRBaseInputInteractor hand)
                hand.SendHapticImpulse(Mathf.Clamp(velocity.magnitude * 0.4f, 0.1f, 0.4f), 0.05f);
        }

        // 놓은 자리에 잠깐 섰다가(HeldItem) 양동이로: 바닥에 쓰러진 대걸레를 찾아다니지 않게 (F6 ③)
        IEnumerator ReturnHome()
        {
            yield return new WaitForSeconds(ReturnDelay);
            returning = null;
            if (!held.IsHeld) held.PlaceAt(home.position, home.rotation);
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
