using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace CardShop.Interaction
{
    /// <summary>
    /// 플레이어 눈높이를 매장 기준 높이(기본 1.6m)에 맞춥니다. 카운터·선반 높이가 이 기준으로 배치되어 있어
    /// 키가 다르거나 앉아서 플레이해도 손이 닿습니다. Camera Offset 높이를 한 번 순간 조정합니다(멀미 영향 적음).
    ///
    /// 다시 맞추는 때: 앱 시작 · 헤드셋을 벗었다 다시 쓸 때(앱 일시정지/포커스 복귀) · 기기 재조정(Recenter) · 디버그 버튼.
    ///
    /// BUG-007: 새 게임마다 다시 맞추던 시절, 고개를 숙인 순간에 맞춰져 시점이 튀었음 → 판마다 맞추지 않음.
    /// BUG-008: ① 리그 루트를 올려서 맞췄더니 중력(GravityProvider)이 다시 바닥으로 내림 → Camera Offset을 올림.
    ///          ② 앱 시작 1초 뒤 한 번만 맞춰서, 자세를 바꾸고 다시 켜면(Quest가 앱을 끄지 않고 이어 실행하는 경우 포함)
    ///          예전 자세 기준 높이가 남았음 → 다시 쓸 때도 맞추고, "고개가 수평이고 머리가 멈춘 순간"에만 맞춤.
    /// </summary>
    public sealed class PlayerHeightCalibrator : MonoBehaviour
    {
        [SerializeField] XROrigin origin;
        [SerializeField] float targetEyeHeight = 1.6f;
        public float TargetEyeHeight => targetEyeHeight;
        [Tooltip("이 시간(초) 동안 고개가 수평이고 머리가 거의 안 움직이면 그 자세로 맞춤")]
        [SerializeField] float stableSeconds = 0.5f;
        [Tooltip("수평으로 보는 고개 각도 한계(도). 숙이거나 젖힌 채로는 맞추지 않음 (BUG-007)")]
        [SerializeField] float maxPitch = 25f;
        [Tooltip("머리가 멈췄다고 보는 위아래 흔들림 한계(m)")]
        [SerializeField] float stableTolerance = 0.03f;
        [Tooltip("안정 자세를 못 찾으면 이 시간(초) 뒤에는 그냥 맞춤")]
        [SerializeField] float giveUpSeconds = 6f;

        Coroutine running;
        bool lostFocus;
        readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();

        public void Configure(XROrigin xrOrigin) => origin = xrOrigin;

        void OnEnable()
        {
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems) s.trackingOriginUpdated += OnTrackingOriginUpdated;
        }

        void OnDisable()
        {
            foreach (var s in subsystems) s.trackingOriginUpdated -= OnTrackingOriginUpdated;
            subsystems.Clear();
        }

        void Start() => CalibrateWhenStable();

        // 헤드셋을 벗으면(근접 센서) 앱이 일시정지되거나 포커스를 잃음. 다시 쓰면 자세가 바뀌었을 수 있음.
        void OnApplicationPause(bool paused)
        {
            if (paused) lostFocus = true;
            else ResumeCheck();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) lostFocus = true;
            else ResumeCheck();
        }

        void ResumeCheck()
        {
            if (!lostFocus) return;
            lostFocus = false;
            CalibrateWhenStable();
        }

        void OnTrackingOriginUpdated(XRInputSubsystem _) => CalibrateWhenStable();

        /// <summary>고개가 수평이고 머리가 멈출 때까지 기다렸다가 맞춥니다.</summary>
        public void CalibrateWhenStable()
        {
            if (!isActiveAndEnabled) return;
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(WaitAndCalibrate());
        }

        IEnumerator WaitAndCalibrate()
        {
            float started = Time.unscaledTime;
            float stableSince = -1f;
            float refHeight = 0f;
            while (Time.unscaledTime - started < giveUpSeconds)
            {
                yield return null;
                if (!TryGetHead(out float headAboveRig, out float pitch)) { stableSince = -1f; continue; }

                bool level = Mathf.Abs(pitch) <= maxPitch;
                if (!level || stableSince < 0f || Mathf.Abs(headAboveRig - refHeight) > stableTolerance)
                {
                    stableSince = level ? Time.unscaledTime : -1f;
                    refHeight = headAboveRig;
                    continue;
                }
                if (Time.unscaledTime - stableSince >= stableSeconds) break;
            }
            Calibrate();
            running = null;
        }

        bool TryGetHead(out float headAboveRig, out float pitch)
        {
            headAboveRig = 0f;
            pitch = 0f;
            if (origin == null || origin.Camera == null) return false;
            Transform cam = origin.Camera.transform;
            headAboveRig = cam.position.y - origin.transform.position.y;
            if (headAboveRig < 0.2f) return false; // 트래킹 전(헤드셋 미착용 등)
            pitch = Mathf.DeltaAngle(0f, cam.eulerAngles.x);
            return true;
        }

        /// <summary>
        /// 지금 자세 그대로 바로 맞춥니다 (디버그 버튼).
        /// 리그 루트가 아니라 Camera Offset의 높이를 바꿉니다. 루트를 들어 올리면 발(CharacterController 바닥)이 공중에 떠서
        /// GravityProvider가 다시 바닥으로 떨어뜨림 → 앉은 플레이어 시점이 도로 낮아졌음 (BUG-008).
        /// </summary>
        public void Calibrate()
        {
            if (!TryGetHead(out float headAboveRig, out _)) return;
            float delta = targetEyeHeight - headAboveRig;
            GameObject offsetObject = origin.CameraFloorOffsetObject;
            Transform target = offsetObject != null ? offsetObject.transform : origin.transform;
            Vector3 p = target.localPosition;
            p.y += delta;
            target.localPosition = p;
        }
    }
}
