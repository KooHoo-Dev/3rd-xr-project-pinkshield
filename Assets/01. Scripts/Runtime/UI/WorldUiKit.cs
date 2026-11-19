using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 월드 공간 UI 조각(캔버스 · 판 · 글자 · 버튼)을 만드는 공용 함수. 에디터 빌더(WorldUi)와
    /// 씬을 다시 빌드하지 않고 스스로 붙는 런타임 화면(진열장 · 슬라임마켓, 집_업그레이드_설계)이 같은 모양을 쓰도록 한 곳에 둡니다.
    /// 캔버스 크기는 1px = 1mm (localScale 0.001).
    /// </summary>
    public static class WorldUiKit
    {
        public static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>레이로 누를 수 있는 월드 캔버스 (widthMm × heightMm). 아래에 "Content"를 만들어 돌려줍니다.</summary>
        public static RectTransform CreateCanvas(string name, Transform parent, float widthMm, float heightMm, out GameObject content)
        {
            var canvasGo = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(widthMm, heightMm);
            rt.localScale = Vector3.one * 0.001f;

            content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)content.transform, Vector2.zero, Vector2.one);
            return rt;
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

        static TMP_FontAsset korean;

        /// <summary>
        /// 한글 글꼴. 런타임에 만드는 화면 · 글자는 모두 이것을 씁니다 (에셋 경로로 불러올 수 없어 씬에 이미 있는 글꼴을 빌림).
        /// 처음 한 번만 씬을 훑고 기억합니다. 못 찾으면 TMP 기본 글꼴(기억하지 않음, 다음에 다시 찾음).
        /// </summary>
        public static TMP_FontAsset KoreanFont
        {
            get
            {
                if (korean != null) return korean;
                foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.font != null && t.font.name.Contains("KR")) return korean = t.font;
                return TMP_Settings.defaultFontAsset;
            }
        }

        // 예전 이름 (shortcut: 맵 수정 작업 중인 ResignLetter가 아직 씀 → 그 작업이 끝나면 KoreanFont로 바꾸고 지움)
        public static TMP_FontAsset FindFont() => KoreanFont;
    }
}
