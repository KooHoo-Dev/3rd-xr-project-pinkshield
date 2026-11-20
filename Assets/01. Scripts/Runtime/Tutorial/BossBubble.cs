using CardShop.UI;
using System;
using CardShop.Core;
using TMPro;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 사장님 말풍선 · 알바생 혼잣말 (월드 TMP + 종이 말풍선 <see cref="PaperBubble"/>: 사장님 = 노랑 실선, 혼잣말 = 하늘색 점선, 왼쪽 위 이름표).
    /// 대상이 있으면 대상 옆, 눈과 대상 사이를 가리지 않는 쪽(시야 가운데 쪽)에 두고 대상을 부드럽게 따라갑니다.
    /// 대상이 없으면 머리 정면에 한 번 놓고 월드에 고정합니다(머리에 붙이지 않음, 멀미).
    /// 회전은 Yaw만 카메라 쪽으로 돌립니다.
    /// </summary>
    public sealed class BossBubble : MonoBehaviour
    {
        const float FadeSeconds = 0.2f;
        const float MoveThreshold = 0.05f;   // 목표가 이만큼 넘게 바뀌어야 움직임 (떨림 방지)
        const float SideSwitchDot = 0.17f;   // 약 10도: 대상이 이만큼 넘어가야 좌우를 바꿈

        [Tooltip("대상 bounds 가장자리에서 말풍선까지 (m)")]
        [SerializeField] float tipGap = 0.25f;
        [Tooltip("눈높이 기준 가장 낮은 높이 (m)")]
        [SerializeField] float minHeightOffset = -0.4f;
        [Tooltip("눈높이 기준 가장 높은 높이 (m)")]
        [SerializeField] float maxHeightOffset = 0.2f;
        [Tooltip("눈에서 말풍선까지 최소 수평 거리 (m)")]
        [SerializeField] float minDistance = 0.6f;
        [Tooltip("대상 없을 때 머리 정면 거리 (m)")]
        [SerializeField] float frontDistance = 1.1f;
        [Tooltip("대상 없을 때 눈높이보다 내리는 높이 (m). 퇴근 · 아침 혼잣말(DayTransition)과 같은 5cm")]
        [SerializeField] float frontDrop = 0.05f;
        [Tooltip("따라가는 빠르기 (클수록 빨리)")]
        [SerializeField] float followSharpness = 6f;
        [Tooltip("글자 크기 (TMP 3D 단위, WorldToast는 0.25)")]
        [SerializeField] float fontSize = 0.28f;
        [Tooltip("글자 영역 너비 (m). 이 너비에서 줄바꿈")]
        [SerializeField] float textWidth = 0.75f;

        TextMeshPro text;
        PaperBubble paper;
        Func<Bounds?> target;
        Vector3 goal;
        float side = 1f;      // +1 = 대상의 오른쪽, -1 = 왼쪽 (보는 사람 기준)
        float shownFor;
        bool showing;

        public bool IsShowing => showing;
        public Vector3 Position => transform.position;

        void Awake()
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            text = go.AddComponent<TextMeshPro>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = true;
            ((RectTransform)go.transform).sizeDelta = new Vector2(textWidth, 0.12f);
            // 맨 위에 그리기: 눈앞에 든 팩 · 진열대에 가려 가끔 안 보였음 (11-20 피드백). 자막과 같은 순서 값
            paper = PaperBubble.Attach(text, new Vector2(0.03f, 0.02f), 0.018f, 100);
            paper.DrawOnTop();
            go.SetActive(false);
        }

        public void Configure(TMP_FontAsset font)
        {
            if (font != null) text.font = font;
        }

        public void Show(TutorialSpeaker speaker, string message, Func<Bounds?> targetBounds)
        {
            target = targetBounds;
            text.gameObject.SetActive(true); // 판을 글자 크기에 맞추려면 켜져 있어야 함
            paper.Show(speaker == TutorialSpeaker.Boss ? BubbleStyle.Boss : BubbleStyle.Self, message ?? string.Empty);
            paper.SetAlpha(0f);
            shownFor = 0f;
            showing = true;

            // 처음 자리는 바로 놓음 (멀리서 날아오지 않게)
            Transform head = Head();
            if (head != null)
            {
                goal = Desired(head, true);
                transform.position = goal;
                FaceCamera(head);
            }
        }

        public void Hide()
        {
            showing = false;
            target = null;
            text.gameObject.SetActive(false);
        }

        /// <summary>대상 없는 말풍선을 지금 머리 정면으로 다시 놓습니다 (헤드셋을 다시 쓴 뒤 등).</summary>
        public void Reposition()
        {
            if (!showing) return;
            Transform head = Head();
            if (head == null) return;
            goal = Desired(head, true);
            transform.position = goal;
            FaceCamera(head);
        }

        void LateUpdate()
        {
            if (!showing) return;
            float dt = Time.unscaledDeltaTime;
            if (shownFor < FadeSeconds)
            {
                shownFor += dt;
                paper.SetAlpha(Mathf.Clamp01(shownFor / FadeSeconds));
            }

            Transform head = Head();
            if (head == null) return;
            if (target != null)
            {
                Vector3 desired = Desired(head, false);
                if ((desired - goal).sqrMagnitude > MoveThreshold * MoveThreshold) goal = desired;
                transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-followSharpness * dt));
            }
            FaceCamera(head);
        }

        // 대상이 있으면 대상 옆, 없으면 (place가 참일 때만) 머리 정면. 대상 없고 place가 거짓이면 지금 목표 유지
        Vector3 Desired(Transform head, bool place)
        {
            Bounds? bounds = target?.Invoke();
            if (!bounds.HasValue)
                return place ? HeadPose.InFront(head, frontDistance, frontDrop).position : goal;

            Bounds b = bounds.Value;
            Vector3 eye = head.position;
            Vector3 toTarget = Vector3.ProjectOnPlane(b.center - eye, Vector3.up);
            if (toTarget.sqrMagnitude < 0.0001f) toTarget = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (toTarget.sqrMagnitude < 0.0001f) toTarget = Vector3.forward;
            Vector3 dir = toTarget.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir);

            // 대상이 시야 오른쪽이면 왼쪽(시야 가운데 쪽)에 둠. 경계 근처에서는 바꾸지 않음
            Vector3 look = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            float lateral = Vector3.Dot(dir, Vector3.Cross(Vector3.up, look));
            if (lateral > SideSwitchDot) side = -1f;
            else if (lateral < -SideSwitchDot) side = 1f;

            Vector3 e = b.extents;
            float halfAcross = Mathf.Abs(e.x * right.x) + Mathf.Abs(e.z * right.z);
            float halfDepth = Mathf.Abs(e.x * dir.x) + Mathf.Abs(e.z * dir.z);
            Vector3 p = b.center
                        + right * (side * (halfAcross + tipGap + textWidth * 0.5f))
                        - dir * halfDepth; // 대상 앞면 높이에 맞춤 (벽 속으로 들어가지 않게)

            // 너무 가까우면 바깥으로 밀어냄
            Vector3 flat = Vector3.ProjectOnPlane(p - eye, Vector3.up);
            if (flat.magnitude < minDistance)
            {
                Vector3 outDir = flat.sqrMagnitude > 0.0001f ? flat.normalized : dir;
                p = new Vector3(eye.x, p.y, eye.z) + outDir * minDistance;
            }
            p.y = Mathf.Clamp(b.center.y, eye.y + minHeightOffset, eye.y + maxHeightOffset);
            return p;
        }

        // TMP 글자는 -Z 쪽에서 읽히므로 forward = 카메라 → 말풍선 (Yaw만)
        void FaceCamera(Transform head)
        {
            Vector3 dir = Vector3.ProjectOnPlane(transform.position - head.position, Vector3.up);
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        static Transform Head()
        {
            Camera cam = Camera.main;
            return cam != null ? cam.transform : null;
        }
    }
}
