using System.Collections;
using CardShop.Core;
using CardShop.Logic;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CardShop.Interaction
{
    /// <summary>
    /// 플레이어 눈높이를 매장 기준 높이(기본 1.6m)에 맞춥니다. 카운터·선반 높이가 이 기준으로 배치되어 있어
    /// 키가 다르거나 앉아서 플레이해도 손이 닿습니다. 새 게임을 시작할 때 자동으로, 디버그 버튼으로 수동 실행합니다.
    /// XR Origin의 루트 높이만 옮기므로 강제 카메라 이동이 아닙니다(한 번 순간 조정, 멀미 영향 적음).
    /// </summary>
    public sealed class PlayerHeightCalibrator : MonoBehaviour
    {
        [SerializeField] XROrigin origin;
        [SerializeField] float targetEyeHeight = 1.6f;
        [Tooltip("앱 시작 후 트래킹이 안정될 때까지 기다리는 시간(초)")]
        [SerializeField] float startupDelay = 1f;

        GameContext context;

        public void Configure(XROrigin xrOrigin) => origin = xrOrigin;

        void OnEnable()
        {
            context = GameContext.Instance;
            if (context != null) context.Events.SessionStarted += OnSessionStarted;
        }

        void OnDisable()
        {
            if (context != null) context.Events.SessionStarted -= OnSessionStarted;
        }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(startupDelay);
            Calibrate();
        }

        void OnSessionStarted(GameSession _) => Calibrate();

        public void Calibrate()
        {
            if (origin == null || origin.Camera == null) return;
            Transform rig = origin.transform;
            float headAboveRig = origin.Camera.transform.position.y - rig.position.y;
            if (headAboveRig < 0.2f) return; // 트래킹 전(헤드셋 미착용 등)에는 건너뜀

            Vector3 p = rig.position;
            p.y = targetEyeHeight - headAboveRig;
            rig.position = p;
        }
    }
}
