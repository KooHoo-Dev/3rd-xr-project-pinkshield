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
    /// 놓으면 1초 뒤 제자리로 돌아가게 합니다. (발밑 노란 양동이 장식은 11-22 M1 사용자 요청으로 뺌 — 보기 전용이라 동작과 무관)
    /// 모양은 빗자루 그대로입니다 (11-20 사용자 결정: 걸레 줄 모양이 화장실 솔처럼 보여 되돌림, 이름만 "대걸레").
    /// 도둑 맞히기(Broom: 머리 위치 · 휘두르는 속도)는 그대로입니다. 얼룩을 문지르면(StainSpot → Scrub) 손이 떨립니다.
    /// </summary>
    [RequireComponent(typeof(Broom), typeof(HeldItem))]
    public sealed class MopStation : MonoBehaviour
    {
        const float ReturnDelay = 1f;

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

        // 놓은 자리에 잠깐 섰다가(HeldItem) 제자리로: 바닥에 쓰러진 대걸레를 찾아다니지 않게 (F6 ③)
        IEnumerator ReturnHome()
        {
            yield return new WaitForSeconds(ReturnDelay);
            returning = null;
            if (!held.IsHeld) held.PlaceAt(home.position, home.rotation);
        }

        static void RenameLabel()
        {
            foreach (TextMeshPro t in FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.text != null && t.text.StartsWith("빗자루")) t.text = "대걸레\n(청소 · 도둑 잡기)"; // 글이 아직 없는(null) 글자도 있음 (11-20 NullReference)
        }
    }
}
