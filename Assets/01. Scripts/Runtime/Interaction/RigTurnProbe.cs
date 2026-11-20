using System.Collections.Generic;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>
    /// 개발 빌드 전용 회전 기록기 (11-20 플레이 B1: 대걸레를 쥐면 화면이 오른쪽으로 계속 돎, 원인 미확정).
    /// 리그(카메라 부모 · 머리 회전 제외)가 최근 1초 동안 30도 넘게 돌았는데 그동안 양 스틱 가로 입력이 거의 0이면,
    /// "무엇을 쥐었는지 · 가까이/레이 · 양 스틱 값 · 회전 속도"를 경고 로그로 남깁니다 (Quest: adb logcat -s Unity, 태그 [RigTurnProbe]).
    /// 고치는 일은 하지 않고 기록만 합니다. 정식 빌드에는 붙지 않습니다(DEVELOPMENT_BUILD · 에디터에서만).
    /// 사용자 메모(Q1): 왼손 그립으로 대걸레를 쥐고, 오른손 스틱으로 오른쪽으로 돌리려 했음 → 스틱을 놓은 뒤에도 도는지가 열쇠.
    /// </summary>
    public sealed class RigTurnProbe : MonoBehaviour
    {
        const float Window = 1f;          // 최근 이만큼(초)의 회전을 봄
        const float TurnLimit = 30f;      // 이보다 많이 돌면 (도)
        const float StickDead = 0.2f;     // 스틱 가로 입력이 이보다 작으면 "입력 없음"
        const float Cooldown = 3f;        // 한 번 남긴 뒤 이만큼은 다시 안 남김 (로그 홍수 방지)

        readonly Queue<(float time, float turn, float stick)> samples = new Queue<(float, float, float)>();
        NearFarInteractor[] hands;
        Transform rig;
        float lastYaw, lastLog = -99f;
        bool hasLast;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin != null && origin.GetComponent<RigTurnProbe>() == null) origin.gameObject.AddComponent<RigTurnProbe>();
        }
#endif

        void Start() => hands = GetComponentsInChildren<NearFarInteractor>(true);

        void LateUpdate()
        {
            // 카메라의 부모 = 머리 움직임을 뺀 리그 방향 (리그 어느 층이 돌아도 잡힘)
            Camera cam = Camera.main;
            if (rig == null) rig = cam != null && cam.transform.parent != null ? cam.transform.parent : transform;
            float yaw = rig.eulerAngles.y;
            float turn = hasLast ? Mathf.DeltaAngle(lastYaw, yaw) : 0f;
            lastYaw = yaw;
            hasLast = true;

            Vector2 left = Stick(XRNode.LeftHand), right = Stick(XRNode.RightHand);
            float now = Time.unscaledTime;
            samples.Enqueue((now, turn, Mathf.Max(Mathf.Abs(left.x), Mathf.Abs(right.x))));
            while (samples.Count > 0 && samples.Peek().time < now - Window) samples.Dequeue();

            float total = 0f, maxStick = 0f;
            foreach ((float _, float t, float s) in samples) { total += t; maxStick = Mathf.Max(maxStick, s); }
            if (Mathf.Abs(total) < TurnLimit || maxStick >= StickDead || now - lastLog < Cooldown) return;
            lastLog = now;
            Debug.LogWarning(Describe(total, left, right));
        }

        string Describe(float total, Vector2 left, Vector2 right)
        {
            var sb = new StringBuilder("[RigTurnProbe] 입력 없이 리그가 돎: ");
            sb.Append($"최근 {Window:0}초 {total:+0;-0}도 ({(total > 0 ? "오른쪽" : "왼쪽")}), 왼 스틱 {left.ToString("F2")} · 오른 스틱 {right.ToString("F2")}");
            if (hands != null)
                foreach (NearFarInteractor h in hands)
                {
                    if (h == null) continue;
                    string held = h.hasSelection ? h.interactablesSelected[0].transform.name : "없음";
                    string region = h.hasSelection ? h.selectionRegion.Value.ToString() : "-";
                    sb.Append($" | {h.name}: 쥔 것 {held} ({region})");
                }
            return sb.ToString();
        }

        static Vector2 Stick(XRNode node)
        {
            InputDevice d = InputDevices.GetDeviceAtXRNode(node);
            return d.isValid && d.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 v) ? v : Vector2.zero;
        }
    }
}
