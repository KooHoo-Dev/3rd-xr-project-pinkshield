using CardShop.Cards.EditorTools;
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
        const string FontPath = DataPaths.Font;
        const int ButtonRowsThatFit = 4; // 패널 아래쪽에 들어가는 목록 버튼 줄 수

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
            WorldUi.Stretch((RectTransform)content.transform, Vector2.zero, Vector2.one);

            Image bg = WorldUi.AddImage(content.transform, "Background", Vector2.zero, Vector2.one, new Color(0.93f, 0.91f, 0.86f));
            bg.raycastTarget = false;

            WorldUi.AddText(content.transform, "Title", "판매", new Vector2(0.04f, 0.95f), new Vector2(0.96f, 0.99f), 30f, new Color(0.16f, 0.12f, 0.2f), font);
            TMP_Text selected = WorldUi.AddText(content.transform, "Selected", "", new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.945f), 26f, new Color(0.16f, 0.12f, 0.2f), font);
            selected.alignment = TextAlignmentOptions.TopLeft;

            float top = 0.728f;
            Button display = WorldUi.AddButton(content.transform, "판매 등록", new Vector2(0.06f, top - ButtonH), new Vector2(0.94f, top), font);
            top -= ButtonH + Gap;
            // 즉시 판매 | 레어 이하 모두 팔기 (11-19 피드백 H) 한 줄에 반씩
            Button sell = WorldUi.AddButton(content.transform, "즉시 판매", new Vector2(0.06f, top - ButtonH), new Vector2(0.49f, top), font);
            Button sellAll = WorldUi.AddButton(content.transform, "레어 이하 모두 팔기", new Vector2(0.51f, top - ButtonH), new Vector2(0.94f, top), font);
            sellAll.GetComponentInChildren<TMP_Text>().fontSize = 22f;

            TMP_Text status = WorldUi.AddText(content.transform, "Status", "", new Vector2(0.04f, 0.50f), new Vector2(0.96f, 0.555f), 20f, new Color(0.35f, 0.32f, 0.4f), font);
            TMP_Text message = WorldUi.AddText(content.transform, "Message", "", new Vector2(0.04f, 0.44f), new Vector2(0.96f, 0.495f), 22f, new Color(0.75f, 0.25f, 0.1f), font);
            WorldUi.AddText(content.transform, "ListHeader", "판매 등록 중 (누르면 취소)", new Vector2(0.04f, 0.395f), new Vector2(0.96f, 0.435f), 20f, new Color(0.35f, 0.32f, 0.4f), font);

            // 목록 버튼 수 = 판매 등록 칸 수 (EconomyConfig가 원본)
            var economy = AssetDatabase.LoadAssetAtPath<CardShop.Cards.EconomyConfig>(DataPaths.Economy);
            int slots = economy != null ? economy.salesDisplaySlots : ButtonRowsThatFit;
            if (slots > ButtonRowsThatFit) Debug.LogWarning($"[SalesPanelBuilder] 판매 등록 칸 {slots}개가 패널에 다 들어가지 않습니다 (최대 {ButtonRowsThatFit}). 패널 높이를 늘려 주세요.");
            var listButtons = new Button[slots];
            float y = 0.385f;
            for (int i = 0; i < slots; i++)
            {
                listButtons[i] = WorldUi.AddButton(content.transform, $"판매 등록 {i + 1}", new Vector2(0.06f, y - ButtonH), new Vector2(0.94f, y), font);
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
            so.FindProperty("sellAllButton").objectReferenceValue = sellAll;
            SerializedProperty list = so.FindProperty("displayedButtons");
            list.arraySize = listButtons.Length;
            for (int i = 0; i < listButtons.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = listButtons[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            content.SetActive(false); // 집에 들어올 때 켜짐
        }
    }
}
