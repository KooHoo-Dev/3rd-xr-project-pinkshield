using TMPro;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 손님 머리 위 말풍선 (2026-11-18 기기 테스트 피드백: 손님이 말을 안 해 단조로움). 대사는 CustomerDirector가 정합니다.
    /// 머리 뼈를 따라다니고, 글자는 플레이어 머리 쪽을 봅니다(WorldToast와 같은 방식). 일시정지 중에는 시간이 흐르지 않습니다.
    /// </summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        const float FadeSeconds = 0.3f;
        // 손님 키를 줄여도 글자 크기는 같게: 크기는 월드 기준으로 맞춤
        const float FontSize = 0.22f;
        const float HeadClearance = 0.32f;

        TextMeshPro text;
        Transform head;
        Transform body;
        float left;
        float duration;

        public bool IsShowing => gameObject.activeSelf;
        /// <summary>일시정지 판단 (CustomerAgent가 넣어 줌)</summary>
        public System.Func<bool> IsPaused { get; set; }

        /// <summary>손님 오브젝트에 말풍선을 만듭니다. head가 없으면 몸 위 1.8m에 띄웁니다.</summary>
        public static SpeechBubble Create(Transform customer, Transform head, TMP_FontAsset font)
        {
            var go = new GameObject("SpeechBubble", typeof(RectTransform));
            // 손님이 사라질 때 함께 사라지게 자식으로 둠. 키 맞춤 배율은 Follow에서 되돌림
            go.transform.SetParent(customer, false);
            var bubble = go.AddComponent<SpeechBubble>();
            bubble.head = head;
            bubble.body = customer;
            bubble.text = go.AddComponent<TextMeshPro>();
            if (font != null) bubble.text.font = font;
            bubble.text.fontSize = FontSize;
            bubble.text.alignment = TextAlignmentOptions.Bottom;
            bubble.text.textWrappingMode = TextWrappingModes.Normal;
            bubble.text.color = new Color(0.12f, 0.1f, 0.14f);
            ((RectTransform)go.transform).sizeDelta = new Vector2(0.6f, 0.2f);
            go.SetActive(false);
            return bubble;
        }

        public void Say(string message, float seconds)
        {
            if (string.IsNullOrEmpty(message)) return;
            // 흰 말풍선 바탕 (TMP mark), 테두리 대신 약간의 여백
            text.text = $"<mark=#FFFFFFE6 padding=\"12,12,6,6\">{message}</mark>";
            text.alpha = 1f;
            duration = Mathf.Max(0.5f, seconds);
            left = duration;
            gameObject.SetActive(true);
            Follow();
        }

        public void Hide() => gameObject.SetActive(false);

        void LateUpdate()
        {
            if (IsPaused == null || !IsPaused()) left -= Time.deltaTime;
            if (left <= 0f) { gameObject.SetActive(false); return; }
            text.alpha = Mathf.Clamp01(left / FadeSeconds);
            Follow();
        }

        void Follow()
        {
            Vector3 top = head != null ? head.position : body.position + Vector3.up * 1.6f;
            transform.position = top + Vector3.up * HeadClearance;

            // 부모(손님)의 키 맞춤 배율을 되돌려 글자 크기를 일정하게
            Vector3 s = body.lossyScale;
            transform.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f / Mathf.Max(0.01f, s.z));

            // TMP 글자는 -Z 쪽에서 읽히므로 forward = 카메라 → 글자 방향
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 dir = transform.position - cam.transform.position;
            dir.y = 0f; // 수평으로만 돌림 (위아래로 기울면 읽기 어려움)
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }
}
