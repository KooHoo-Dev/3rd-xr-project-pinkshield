using CardShop.Presentation;
using CardShop.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 바인더 옆의 판매 패널(판매 등록 · 즉시 판매 · 판매 등록 목록)을 만듭니다. ShopSceneBuilder.Build가 BinderBuilder.Create 뒤에 호출합니다.
    /// 바인더 루트("Binder")의 자식으로 두어 바인더와 같은 방향 · 같은 기울기로 왼쪽에 놓입니다 (월드 고정, 머리에 붙지 않음).
    /// </summary>
    public static class SalesPanelBuilder
    {
        const string FontPath = "Assets/99. External Assets/Font/Noto_Sans_KR/NotoSansKR-Regular SDF.asset";
        const int DisplayedButtonCount = 4; // EconomyConfig.salesDisplaySlots 기본값과 같게

        // 패널 캔버스 크기 (1px = 1mm). 버튼 높이 56mm · 간격 20mm (시연 피드백 C2).
        const float PanelWidth = 440f;
        const float PanelHeight = 820f;
        const float ButtonH = 56f / PanelHeight;
        const float Gap = 20f / PanelHeight;

        static TMP_FontAsset font;

        public static void Create()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var binder = Object.FindAnyObjectByType<BinderView>();
            if (binder == null)
            {
                Debug.LogError("[SalesPanelBuilder] BinderView가 없습니다. BinderBuilder.Create 뒤에 호출하세요.");
                return;
            }

            var canvasGo = new GameObject("SalesPanel", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            canvasGo.transform.SetParent(binder.transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rt.localScale = Vector3.one * 0.001f;
            rt.localRotation = Quaternion.Euler(20f, 0f, 0f); // 바인더 페이지와 같은 독서대 각도
            rt.localPosition = new Vector3(-(0.3f + 0.03f + PanelWidth * 0.0005f), PanelHeight * 0.0005f + 0.012f, 0f); // 바인더 페이지 왼쪽

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)content.transform, Vector2.zero, Vector2.one);

            Image bg = AddImage(content.transform, "Background", Vector2.zero, Vector2.one, new Color(0.93f, 0.91f, 0.86f));
            bg.raycastTarget = false;

            AddText(content.transform, "Title", "판매", new Vector2(0.04f, 0.95f), new Vector2(0.96f, 0.99f), 30f, new Color(0.16f, 0.12f, 0.2f));
            TMP_Text selected = AddText(content.transform, "Selected", "", new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.945f), 26f, new Color(0.16f, 0.12f, 0.2f));
            selected.alignment = TextAlignmentOptions.TopLeft;

            float top = 0.728f;
            Button display = CreateUiButton(content.transform, "판매 등록", new Vector2(0.06f, top - ButtonH), new Vector2(0.94f, top));
            top -= ButtonH + Gap;
            Button sell = CreateUiButton(content.transform, "즉시 판매", new Vector2(0.06f, top - ButtonH), new Vector2(0.94f, top));

            TMP_Text status = AddText(content.transform, "Status", "", new Vector2(0.04f, 0.50f), new Vector2(0.96f, 0.555f), 20f, new Color(0.35f, 0.32f, 0.4f));
            TMP_Text message = AddText(content.transform, "Message", "", new Vector2(0.04f, 0.44f), new Vector2(0.96f, 0.495f), 22f, new Color(0.75f, 0.25f, 0.1f));
            AddText(content.transform, "ListHeader", "판매 등록 중 (누르면 취소)", new Vector2(0.04f, 0.395f), new Vector2(0.96f, 0.435f), 20f, new Color(0.35f, 0.32f, 0.4f));

            var listButtons = new Button[DisplayedButtonCount];
            float y = 0.385f;
            for (int i = 0; i < DisplayedButtonCount; i++)
            {
                listButtons[i] = CreateUiButton(content.transform, $"판매 등록 {i + 1}", new Vector2(0.06f, y - ButtonH), new Vector2(0.94f, y));
                listButtons[i].GetComponentInChildren<TMP_Text>().fontSize = 24f;
                y -= ButtonH + Gap;
            }

            var panel = canvasGo.AddComponent<SalesPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("content").objectReferenceValue = content;
            so.FindProperty("binder").objectReferenceValue = binder;
            so.FindProperty("selectedLabel").objectReferenceValue = selected;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("messageLabel").objectReferenceValue = message;
            so.FindProperty("displayButton").objectReferenceValue = display;
            so.FindProperty("sellButton").objectReferenceValue = sell;
            SerializedProperty list = so.FindProperty("displayedButtons");
            list.arraySize = listButtons.Length;
            for (int i = 0; i < listButtons.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = listButtons[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            content.SetActive(false); // 집에 들어올 때 켜짐
        }

        // ------------------------------------------------------------------ 도우미 (BinderBuilder와 같은 방식)

        static Image AddImage(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, min, max);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        static TMP_Text AddText(Transform parent, string name, string text, Vector2 min, Vector2 max, float size, Color color)
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

        // ShopSceneBuilder.CreateUiButton과 같은 모양 (그쪽 함수는 private이라 BinderBuilder처럼 같은 규칙으로 따로 둠)
        static Button CreateUiButton(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            go.name = "Button_" + text;
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, anchorMin, anchorMax);
            var label = go.GetComponentInChildren<TMP_Text>();
            label.text = text;
            label.fontSize = 28f;
            label.color = Color.white;
            if (font != null) label.font = font;

            var button = go.GetComponent<Button>();
            button.targetGraphic.color = Color.white;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = new Color(0.28f, 0.22f, 0.42f, 1f),
                highlightedColor = new Color(0.95f, 0.55f, 0.15f, 1f),
                pressedColor = new Color(0.65f, 0.28f, 0.05f, 1f),
                selectedColor = new Color(0.28f, 0.22f, 0.42f, 1f),
                disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.05f,
            };
            go.AddComponent<UiHoverFeedback>();
            return button;
        }

        static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
