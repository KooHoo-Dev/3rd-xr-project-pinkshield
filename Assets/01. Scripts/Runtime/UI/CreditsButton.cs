using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 타이틀 판 왼쪽 아래의 [크레딧] 버튼 (11-19, 음악 출처 표기). 누르면 판 위에 크레딧 판을 덮고, 버튼은 [닫기]가 됩니다.
    /// 문구는 Resources/Credits.txt 하나에서 읽습니다 (README도 같은 파일을 가리킴).
    /// 씬을 다시 빌드하지 않도록 실행 중에 [눈높이 맞추기] 버튼을 복제해 만듭니다 (TutorialTitleToggle과 같은 방식).
    /// </summary>
    public sealed class CreditsButton : MonoBehaviour
    {
        const string CreditsResource = "Credits";

        [Tooltip("버튼 자리 (타이틀 판 비율: 왼쪽 아래)")]
        [SerializeField] Vector2 anchorMin = new Vector2(0.03f, 0.20f);
        [Tooltip("버튼 자리 (타이틀 판 비율: 오른쪽 위)")]
        [SerializeField] Vector2 anchorMax = new Vector2(0.22f, 0.32f);
        [SerializeField] float fontSize = 26f;
        [SerializeField] float creditsFontSize = 17f;

        Button button;
        TMP_Text label;
        GameObject panel;

        public void Configure(TitleBoard board)
        {
            if (board == null || board.RecalibrateButton == null) return;
            Button source = board.RecalibrateButton;
            GameObject go = Instantiate(source.gameObject, source.transform.parent, false);
            go.name = "Button_Credits";
            WorldUiKit.Stretch((RectTransform)go.transform, anchorMin, anchorMax);
            button = go.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Toggle);
            label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.fontSize = fontSize;

            panel = BuildPanel(source.transform.parent, label != null ? label.font : null, creditsFontSize);
            panel.SetActive(false);
            go.transform.SetAsLastSibling(); // 크레딧 판이 덮어도 [닫기]는 눌리게
            UpdateLabel();
        }

        static GameObject BuildPanel(Transform parent, TMP_FontAsset font, float maxSize)
        {
            var asset = Resources.Load<TextAsset>(CreditsResource);
            string text = asset != null ? asset.text : "Credits.txt가 없습니다.";

            Color opaque = UiTheme.Panel;
            opaque.a = 1f; // 뒤의 게임 이름이 비치지 않게
            Image bg = WorldUiKit.AddImage(parent, "CreditsPanel", Vector2.zero, Vector2.one, opaque);
            bg.raycastTarget = true; // 뒤의 버튼이 눌리지 않게
            WorldUiKit.AddText(bg.transform, "Heading", "크레딧", new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.97f), 40f, UiTheme.Gold, font)
                .fontStyle = FontStyles.Bold;
            TMP_Text body = WorldUiKit.AddText(bg.transform, "Body", text, new Vector2(0.04f, 0.34f), new Vector2(0.96f, 0.86f), maxSize, UiTheme.Text, font);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true; // 문구가 늘어나도 판 밖으로 넘치지 않게
            body.fontSizeMin = 10f;
            body.fontSizeMax = maxSize;
            return bg.gameObject;
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Toggle);
        }

        void Toggle()
        {
            if (panel == null) return;
            panel.SetActive(!panel.activeSelf);
            if (panel.activeSelf)
            {
                // [안내 켬/끔](TutorialTitleToggle)은 이 판보다 늦게 붙어 위에 그려지므로, 열 때마다 맨 위로 올림
                panel.transform.SetAsLastSibling();
                button.transform.SetAsLastSibling();
            }
            UpdateLabel();
        }

        void UpdateLabel()
        {
            if (label != null) label.text = panel != null && panel.activeSelf ? "닫기" : "크레딧";
        }
    }
}
