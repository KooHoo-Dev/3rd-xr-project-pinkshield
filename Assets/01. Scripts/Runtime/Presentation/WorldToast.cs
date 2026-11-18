using TMPro;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 월드 공간 말풍선 하나. 지정한 자리에 짧은 안내를 띄우고 잠시 뒤 사라집니다.
    /// 디버그 패널 글자만으로는 "반응 없음"과 구분되지 않아서 만들었습니다 (개봉연출 설계 5장 #1).
    /// 위치는 월드 고정이고, 글자 방향만 머리 쪽으로 돌립니다.
    /// 떠 있는 시간은 글자 수에 맞춥니다 (11-19 플레이 피드백: 긴 혼잣말을 다 읽기 전에 사라짐).
    /// </summary>
    public sealed class WorldToast : MonoBehaviour
    {
        const float MinSeconds = 2.2f;
        const float MaxSeconds = 7f;
        const float SecondsPerChar = 0.12f; // VR에서 한글 읽는 속도를 넉넉히 (초당 약 8자)
        const float FadeSeconds = 0.3f;

        TextMeshPro text;
        float hideAt;

        public static WorldToast Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("WorldToast", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var toast = go.AddComponent<WorldToast>();
            toast.text = go.AddComponent<TextMeshPro>();
            if (font != null) toast.text.font = font;
            toast.text.fontSize = 0.25f;
            toast.text.alignment = TextAlignmentOptions.Center;
            toast.text.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)go.transform).sizeDelta = new Vector2(0.4f, 0.06f);
            go.SetActive(false);
            return toast;
        }

        public void Show(string message, Vector3 position)
        {
            transform.position = position;
            // 배경을 더 진하게 + 글자 둘레 여백 (11-19 피드백: 밝은 배경 앞에서 잘 안 보임)
            text.text = $"<mark=#140F1EF0 padding=\"6,6,3,3\">{message}</mark>";
            text.alpha = 1f;
            hideAt = Time.time + SecondsFor(message);
            gameObject.SetActive(true);
            FaceCamera();
        }

        /// <summary>글자 수(태그 · 공백 제외)에 맞춘 표시 시간.</summary>
        public static float SecondsFor(string message)
        {
            if (string.IsNullOrEmpty(message)) return MinSeconds;
            int chars = 0;
            bool inTag = false;
            foreach (char c in message)
            {
                if (c == '<') inTag = true;
                else if (c == '>') inTag = false;
                else if (!inTag && !char.IsWhiteSpace(c)) chars++;
            }
            return Mathf.Clamp(1f + chars * SecondsPerChar, MinSeconds, MaxSeconds);
        }

        void LateUpdate()
        {
            float left = hideAt - Time.time;
            if (left <= 0f) { gameObject.SetActive(false); return; }
            text.alpha = Mathf.Clamp01(left / FadeSeconds);
            FaceCamera();
        }

        // TMP 글자는 -Z 쪽에서 읽히므로 forward = 카메라 → 글자 방향
        void FaceCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 dir = transform.position - cam.transform.position;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }
}
