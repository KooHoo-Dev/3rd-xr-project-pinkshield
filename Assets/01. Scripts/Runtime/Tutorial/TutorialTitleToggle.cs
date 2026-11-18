using CardShop.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 타이틀 판 오른쪽 아래의 [안내 켬 / 안내 끔] 버튼 (튜토리얼_설계 9장 Q3). 값은 저장하지 않고 앱을 켤 때마다 "켬"입니다.
    /// 씬을 다시 빌드하지 않도록 실행 중에 [눈높이 맞추기] 버튼을 복제해 만듭니다.
    /// </summary>
    public sealed class TutorialTitleToggle : MonoBehaviour
    {
        [Tooltip("버튼 자리 (타이틀 판 비율: 왼쪽 아래)")]
        [SerializeField] Vector2 anchorMin = new Vector2(0.78f, 0.20f);
        [Tooltip("버튼 자리 (타이틀 판 비율: 오른쪽 위)")]
        [SerializeField] Vector2 anchorMax = new Vector2(0.97f, 0.32f);
        [SerializeField] float fontSize = 26f;

        Button button;
        TMP_Text label;

        public void Configure(TitleBoard board)
        {
            if (board == null || board.RecalibrateButton == null) return;
            Button source = board.RecalibrateButton;
            GameObject go = Instantiate(source.gameObject, source.transform.parent, false);
            go.name = "Button_TutorialToggle";
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            button = go.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Toggle);
            label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.fontSize = fontSize;
            UpdateLabel();
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Toggle);
        }

        void Toggle()
        {
            TutorialSwitch.Enabled = !TutorialSwitch.Enabled;
            UpdateLabel();
        }

        void UpdateLabel()
        {
            if (label != null) label.text = TutorialSwitch.Enabled ? "안내 켬" : "안내 끔";
        }
    }
}
