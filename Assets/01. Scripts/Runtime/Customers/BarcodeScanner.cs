using System.Collections;
using CardShop.Cards;
using CardShop.Presentation;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Customers
{
    /// <summary>
    /// 손에 드는 바코드 스캐너 (플레이 피드백: 계산 업무 디테일). 잡은 채 트리거를 당기면 앞쪽으로 레이저를 쏘아
    /// 닿은 ScannableItem을 찍습니다. 찍히면 삑 소리 + 진동.
    /// 놓으면 그 자리에 떠 있다가 잠시 뒤 카운터의 제자리로 돌아갑니다(바닥에 떨어져 못 줍는 일 방지, 물리 없음).
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class BarcodeScanner : MonoBehaviour
    {
        [Tooltip("레이저가 나가는 곳. forward 방향으로 쏨")]
        [SerializeField] Transform tip;
        [SerializeField] float range = 0.45f;
        [SerializeField] LineRenderer laser;
        [SerializeField] float returnDelay = 2f;
        [SerializeField] float beepVolume = 0.6f;

        XRGrabInteractable grab;
        Vector3 homePos;
        Quaternion homeRot;
        Coroutine returnRoutine;
        static AudioClip beep;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            homePos = transform.position;
            homeRot = transform.rotation;
            if (laser != null) laser.enabled = false;
        }

        void OnEnable()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
            grab.activated.AddListener(OnTrigger);
        }

        void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
            grab.activated.RemoveListener(OnTrigger);
        }

        void OnGrabbed(SelectEnterEventArgs _)
        {
            if (returnRoutine != null) { StopCoroutine(returnRoutine); returnRoutine = null; }
            if (laser != null) laser.enabled = true;
        }

        void OnReleased(SelectExitEventArgs _)
        {
            if (laser != null) laser.enabled = false;
            returnRoutine = StartCoroutine(ReturnHome());
        }

        IEnumerator ReturnHome()
        {
            yield return new WaitForSeconds(returnDelay);
            transform.SetPositionAndRotation(homePos, homeRot);
            returnRoutine = null;
        }

        // 잡고 있는 동안 레이저 길이를 닿는 곳까지 맞춤 (어디를 겨누는지 보이게)
        void Update()
        {
            if (laser == null || !laser.enabled || tip == null) return;
            float length = Physics.Raycast(tip.position, tip.forward, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Collide)
                ? hit.distance : range;
            laser.SetPosition(0, tip.position);
            laser.SetPosition(1, tip.position + tip.forward * length);
        }

        void OnTrigger(ActivateEventArgs args)
        {
            if (tip == null) return;
            RaycastHit[] hits = Physics.RaycastAll(tip.position, tip.forward, range, ~0, QueryTriggerInteraction.Collide);
            foreach (RaycastHit hit in hits)
            {
                var item = hit.collider.GetComponentInParent<ScannableItem>();
                if (item == null || !item.TryScan()) continue;

                if (Sfx.Has(SoundId.ScanBeep)) Sfx.Play(SoundId.ScanBeep, tip.position);
                else AudioSource.PlayClipAtPoint(Beep(), tip.position, beepVolume); // SoundBank 칸이 비면 코드로 만든 삑
                if (args.interactorObject is XRBaseInputInteractor hand) hand.SendHapticImpulse(0.5f, 0.08f);
                return;
            }
        }

        // 계산대 "삑" (외부 음원 없이 만든 1.8kHz 짧은 사인파)
        static AudioClip Beep()
        {
            if (beep != null) return beep;
            const int rate = 44100;
            const float seconds = 0.12f, freq = 1800f;
            int n = (int)(rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float env = Mathf.Min(1f, Mathf.Min(i, n - i) / (rate * 0.005f)); // 딸깍 소리 방지
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / rate) * 0.5f * env;
            }
            beep = AudioClip.Create("ScannerBeep", n, 1, rate, false);
            beep.SetData(data, 0);
            return beep;
        }
    }
}
