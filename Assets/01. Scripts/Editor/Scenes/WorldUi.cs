using CardShop.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 월드 공간 UI 조각(버튼 · 글자 · 그림)을 만드는 빌더 공용 함수.
    /// 일시정지 메뉴 · 디버그 패널 · 바인더 · 판매 패널이 같은 버튼 색과 글자 크기를 쓰도록 한 곳에 둡니다.
    /// </summary>
    internal static class WorldUi
    {
        public static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static Image AddImage(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, min, max);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>레이를 받지 않는 글자 (뒤의 버튼 클릭을 가리지 않게).</summary>
        public static TMP_Text AddText(Transform parent, string name, string text, Vector2 min, Vector2 max, float size, Color color, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, min, max);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
            if (font != null) label.font = font;
            return label;
        }

        /// <summary>테마 판 바탕 + 위쪽 분홍 띠(높이 accentMm). 바탕을 돌려줌 (레이는 받지 않음).</summary>
        public static Image AddPanel(Transform parent, float accentMm = 8f)
        {
            Image bg = AddImage(parent, "Background", Vector2.zero, Vector2.one, UiTheme.Panel);
            bg.raycastTarget = false;
            Image accent = AddImage(parent, "AccentBar", new Vector2(0f, 1f), Vector2.one, UiTheme.Accent);
            ((RectTransform)accent.transform).offsetMin = new Vector2(0f, -accentMm);
            accent.raycastTarget = false;
            return bg;
        }

        public static Button AddButton(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax, TMP_FontAsset font)
        {
            GameObject go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            go.name = "Button_" + text;
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, anchorMin, anchorMax);
            var label = go.GetComponentInChildren<TMP_Text>();
            label.text = text;
            label.fontSize = 28f;
            label.color = UiTheme.Text;
            if (font != null) label.font = font;

            // 레이가 닿으면 색이 확 바뀌게 (시연 피드백 C3: 기본 하이라이트는 거의 흰색이라 안 보였음)
            var button = go.GetComponent<Button>();
            button.targetGraphic.color = Color.white;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = UiTheme.ButtonNormal,
                highlightedColor = UiTheme.ButtonHover,
                pressedColor = UiTheme.ButtonPressed,
                selectedColor = UiTheme.ButtonNormal,
                disabledColor = UiTheme.ButtonDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.05f,
            };
            go.AddComponent<UiHoverFeedback>();
            return button;
        }
    }
}
