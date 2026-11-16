using TMPro;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 월드 공간 말풍선 하나. 지정한 자리에 짧은 안내를 띄우고 잠시 뒤 사라집니다.
    /// 디버그 패널 글자만으로는 "반응 없음"과 구분되지 않아서 만들었습니다 (개봉연출 설계 5장 #1).
    /// 위치는 월드 고정이고, 글자 방향만 머리 쪽으로 돌립니다.
    /// </summary>
    public sealed class WorldToast : MonoBehaviour
    {
        const float Duration = 1.8f;
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
            text.text = $"<mark=#140F1ECC>{message}</mark>";
            text.alpha = 1f;
            hideAt = Time.time + Duration;
            gameObject.SetActive(true);
            FaceCamera();
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
