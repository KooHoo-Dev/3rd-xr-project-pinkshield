using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>
    /// 자르기 개봉 (설계서 7-2 TearGestureOpen, 개봉연출 설계 2장 ②).
    /// 1) 왼손 그립으로 팩을 잡으면 팩이 머리 정면에 떠서 고정됩니다(월드 고정, 잡은 순간의 시선 기준).
    /// 2) 오른손 트리거를 누른 채 팩 윗부분의 가이드 선을 따라 가로로 그으면 자른 만큼 선이 채워집니다.
    ///    자르는 지점 = 오른손 끝이 팩 가까이 있으면 그 위치, 아니면 오른손 레이저가 팩 면에 닿는 점 (BUG-002).
    ///    끊어 그어도 이어지며, 팩 너비의 cutThreshold 이상 자르면 개봉 요청.
    /// 3) 왼손 그립을 놓으면 고정이 풀리고 자르던 진행도도 초기화됩니다.
    /// 오른손으로 잡은 팩은 일반 물건처럼 들기만 합니다.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable), typeof(BoxCollider))]
    public sealed class TearGestureOpen : MonoBehaviour, IPackOpenMethod
    {
        [Header("눈앞 고정")]
        [Tooltip("너무 가까우면(35cm) 눈이 피로해 45cm로 늘리고, 대신 크기를 키움 (시연 피드백 A1)")]
        [SerializeField] float presentDistance = 0.45f;
        [SerializeField] float presentDrop = 0.08f;
        [Tooltip("눈앞에 떠 있는 동안의 팩 크기 배율. 자르기 판정은 팩 로컬 좌표라 함께 커짐")]
        [SerializeField] float presentScale = 1.5f;

        [Header("자르기")]
        [Tooltip("팩 너비 중 이만큼 자르면 개봉")]
        [SerializeField, Range(0.3f, 1f)] float cutThreshold = 0.7f;
        [Tooltip("자르는 선 위아래로 이 거리 안이면 인정 (m)")]
        [SerializeField] float cutBandHeight = 0.04f;
        [Tooltip("손끝으로 자를 때: 팩 앞뒤로 이 거리 안이면 인정 (m)")]
        [SerializeField] float cutBandDepth = 0.08f;
        [Tooltip("레이저로 자를 때: 손에서 팩까지 이 거리 안이면 인정 (m)")]
        [SerializeField] float rayMaxDistance = 0.8f;
        [Tooltip("자르는 선의 위치: 팩 윗변에서 아래로 (m)")]
        [SerializeField] float cutLineFromTop = 0.012f;

        static readonly Color GuideColor = new Color(1f, 1f, 1f, 1f);
        static readonly Color CutColor = new Color(1f, 0.85f, 0.2f, 1f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public event Action Requested;

        XRGrabInteractable grab;
        Rigidbody body;
        RigidbodyInterpolation interpolationBeforePresent;
        float width, cutY;
        bool presented;
        Vector3 scaleBeforePresent = Vector3.one;
        XRBaseInputInteractor cutter;
        float cutMin, cutMax;
        int lastTick;
        Transform guideLine, cutLine;

        public bool IsPresented => presented;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            var box = GetComponent<BoxCollider>();
            width = box.size.x;
            cutY = box.center.y + box.size.y * 0.5f - cutLineFromTop;
            guideLine = CreateLine("CutGuide", GuideColor, 0.0015f);
            cutLine = CreateLine("CutProgress", CutColor, 0.003f);
            ShowLines(false);
        }

        void OnEnable()
        {
            grab.selectEntered.AddListener(OnSelectEntered);
            grab.selectExited.AddListener(OnSelectExited);
        }

        void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnSelectEntered);
            grab.selectExited.RemoveListener(OnSelectExited);
            if (presented) EndPresent();
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (args.interactorObject.handedness == InteractorHandedness.Left) BeginPresent();
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (presented && args.interactorObject.handedness == InteractorHandedness.Left) EndPresent();
        }

        void BeginPresent()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            presented = true;
            // XRI가 팩을 손 위치로 끌고 가지 않게 하고, 우리가 정한 자리에 둡니다.
            grab.trackPosition = false;
            grab.trackRotation = false;

            Transform head = cam.transform;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = head.up; // 바로 아래를 보고 있을 때
            forward.Normalize();
            // 앞면(-Z)이 플레이어를 향하고 윗변(+Y)이 위
            Vector3 pos = head.position + forward * presentDistance + Vector3.down * presentDrop;
            Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);
            transform.SetPositionAndRotation(pos, rot);
            scaleBeforePresent = transform.localScale;
            transform.localScale = scaleBeforePresent * presentScale;
            if (body != null)
            {
                // Transform만 옮기면 Rigidbody 보간이 손 위치로 되돌려 놓을 수 있습니다 (BUG-003과 같은 원인).
                interpolationBeforePresent = body.interpolation;
                body.interpolation = RigidbodyInterpolation.None;
                body.position = pos;
                body.rotation = rot;
            }

            cutter = HandFinder.Find(InteractorHandedness.Right, grab.interactionManager);
            if (cutter == null) Debug.LogWarning("[TearGestureOpen] 오른손 인터랙터를 찾지 못해 자를 수 없습니다.", this);
            ResetCut();
            ShowLines(true);
        }

        void EndPresent()
        {
            presented = false;
            transform.localScale = scaleBeforePresent;
            grab.trackPosition = true;
            grab.trackRotation = true;
            if (body != null) body.interpolation = interpolationBeforePresent;
            cutter = null;
            ResetCut();
            ShowLines(false);
        }

        void Update()
        {
            if (!presented || cutter == null) return;

            if (!cutter.activateInput.ReadIsPerformed() || !TryGetCutPoint(out Vector3 local)) return;

            float x = Mathf.Clamp(local.x, -width * 0.5f, width * 0.5f);
            if (cutMax < cutMin) cutMin = cutMax = x; // 첫 획
            cutMin = Mathf.Min(cutMin, x);
            cutMax = Mathf.Max(cutMax, x);
            UpdateCutLine();

            float progress = (cutMax - cutMin) / width;
            int tick = Mathf.FloorToInt(progress * 10f);
            if (tick > lastTick)
            {
                lastTick = tick;
                cutter.SendHapticImpulse(0.25f + progress * 0.3f, 0.03f); // 자를수록 조금씩 세짐
            }

            if (progress >= cutThreshold)
            {
                ResetCut();
                Requested?.Invoke(); // 개봉되면 팩이 사라지고, 거부되면(영업 중 등) 다시 자를 수 있음
            }
        }

        // 컨트롤러 원점(손잡이 중앙)은 팩에 닿기 전에 컨트롤러 몸체가 먼저 닿아서, 원점만 보면 거의 인정되지 않았습니다.
        bool TryGetCutPoint(out Vector3 local)
        {
            var nearFar = cutter as NearFarInteractor;

            // 1) 손끝(근접 판정 지점)이 팩 가까이 있을 때
            Transform tip = nearFar != null && nearFar.nearInteractionCaster != null ? nearFar.nearInteractionCaster.effectiveCastOrigin : null;
            if (tip != null)
            {
                local = transform.InverseTransformPoint(tip.position);
                if (InCutBand(local) && Mathf.Abs(local.z) <= cutBandDepth) return true;
            }

            // 2) 레이저(조준 방향)가 팩 면(로컬 z = 0)에 닿는 점
            Transform aim = nearFar != null && nearFar.farInteractionCaster != null ? nearFar.farInteractionCaster.effectiveCastOrigin : cutter.transform;
            Vector3 origin = transform.InverseTransformPoint(aim.position);
            Vector3 dir = transform.InverseTransformDirection(aim.forward);
            if (Mathf.Abs(dir.z) > 0.0001f)
            {
                float t = -origin.z / dir.z;
                local = origin + dir * t;
                if (t > 0f && t * transform.lossyScale.z <= rayMaxDistance && InCutBand(local)) return true;
            }

            local = default;
            return false;
        }

        bool InCutBand(Vector3 p) => Mathf.Abs(p.y - cutY) <= cutBandHeight && Mathf.Abs(p.x) <= width * 0.5f + 0.03f;

        void ResetCut()
        {
            cutMin = 1f;
            cutMax = -1f; // max < min = 아직 안 자름
            lastTick = 0;
            UpdateCutLine();
        }

        void UpdateCutLine()
        {
            bool any = cutMax > cutMin;
            cutLine.gameObject.SetActive(presented && any);
            if (!any) return;
            cutLine.localPosition = new Vector3((cutMin + cutMax) * 0.5f, cutY, cutLine.localPosition.z);
            cutLine.localScale = new Vector3(cutMax - cutMin, cutLine.localScale.y, cutLine.localScale.z);
        }

        void ShowLines(bool on)
        {
            guideLine.gameObject.SetActive(on);
            if (!on) cutLine.gameObject.SetActive(false);
        }

        // 팩 앞면(-Z) 바로 위에 얇은 막대. 머티리얼은 팩 것을 빌려 쓰고 색만 바꿉니다(빌드에서 셰이더가 빠지지 않도록).
        Transform CreateLine(string lineName, Color color, float thickness)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = lineName;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            float frontZ = -GetComponent<BoxCollider>().size.z * 0.5f - 0.001f;
            go.transform.localPosition = new Vector3(0f, cutY, frontZ);
            go.transform.localScale = new Vector3(width, thickness, 0.0008f);

            var r = go.GetComponent<MeshRenderer>();
            Renderer source = GetComponentInChildren<MeshRenderer>();
            if (source != null && source != r) r.sharedMaterial = source.sharedMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, color);
            r.SetPropertyBlock(mpb);
            return go.transform;
        }
    }
}
