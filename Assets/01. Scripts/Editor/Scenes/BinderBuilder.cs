using CardShop.Cards;
using CardShop.Cards.EditorTools;
using CardShop.Interaction;
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
    /// 집 책상 위의 바인더(보관함 겸 도감)를 만듭니다. ShopSceneBuilder.Build가 집 구역을 만든 뒤 호출합니다.
    /// - 세워 둔 바인더 페이지 = 월드 UI 1장 (한 페이지 Columns x Rows 칸, 레이 또는 손가락으로 누름)
    /// - 그 옆에 눕힌 표지 = 소켓 1개 (카드를 놓으면 자기 칸으로 날아감)
    /// </summary>
    public static class BinderBuilder
    {
        const string FontPath = "Assets/99. External Assets/Font/Noto_Sans_KR/NotoSansKR-Regular SDF.asset";
        const string StylesPath = DataPaths.CardsDir + "/CardRarityStyles.asset";
        const string HomeRoot = ShopSceneBuilder.HomeRootName;

        // 한 페이지 칸 수. 카드 수(50)·페이지 수는 CardDatabase에서 계산합니다 (BinderLayout).
        // 9칸이면 칸 오브젝트 9개를 페이지마다 재사용 (Quest 성능, 설계서 7-3).
        const int Columns = 3;
        const int Rows = 3;

        // 페이지 캔버스 크기 (1px = 1mm)
        const float PageWidth = 600f;
        const float PageHeight = 760f;

        static TMP_FontAsset font;

        public static void Create()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            GameObject desk = GameObject.Find(HomeRoot + "/Desk");
            if (desk == null)
            {
                Debug.LogError("[BinderBuilder] 집 구역의 Desk가 없습니다. ShopSceneBuilder.CreateHome 뒤에 호출하세요.");
                return;
            }
            Vector3 deskPos = desk.transform.position;
            Vector3 deskSize = desk.transform.localScale;
            float deskTop = deskPos.y + deskSize.y / 2f;

            // 바인더 페이지가 집 시작 위치(HomeSpawn)를 바라보도록 돌림
            Quaternion facing = Quaternion.identity;
            GameObject spawn = GameObject.Find(HomeRoot + "/HomeSpawn");
            if (spawn != null)
            {
                Vector3 away = deskPos - spawn.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.0001f) facing = Quaternion.LookRotation(away);
            }

            var root = new GameObject("Binder");
            root.transform.SetParent(desk.transform.parent, true);
            root.transform.SetPositionAndRotation(new Vector3(deskPos.x - 0.2f, deskTop, deskPos.z), facing);

            BinderView view = CreatePage(root.transform);
            BinderCoverSocket socket = CreateCover(desk.transform.parent, new Vector3(deskPos.x + 0.38f, deskTop, deskPos.z - 0.05f));

            var so = new SerializedObject(view);
            so.FindProperty("coverSocket").objectReferenceValue = socket;
            so.ApplyModifiedPropertiesWithoutUndo();

            RenameDeskLabel(desk.transform.parent);
        }

        // ------------------------------------------------------------------ 페이지 (월드 UI)

        static BinderView CreatePage(Transform root)
        {
            Quaternion tilt = Quaternion.Euler(20f, 0f, 0f); // 윗부분이 뒤로 살짝 누운 독서대 각도
            float halfHeight = PageHeight * 0.001f / 2f;

            // 바인더 두꺼운 표지(뒤판): 페이지 바로 뒤
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "BinderBoard";
            board.transform.SetParent(root, false);
            board.transform.localRotation = tilt;
            board.transform.localPosition = new Vector3(0f, halfHeight + 0.012f, 0f) + tilt * new Vector3(0f, 0f, 0.012f);
            board.transform.localScale = new Vector3(PageWidth * 0.001f + 0.03f, PageHeight * 0.001f + 0.03f, 0.02f);
            board.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.18f, 0.22f, 0.42f) };

            var canvasGo = new GameObject("BinderPage", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            canvasGo.transform.SetParent(root, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(PageWidth, PageHeight);
            rt.localScale = Vector3.one * 0.001f;
            rt.localRotation = tilt;
            rt.localPosition = new Vector3(0f, halfHeight + 0.012f, 0f);

            Image bg = AddImage(canvasGo.transform, "Background", Vector2.zero, Vector2.one, new Color(0.93f, 0.91f, 0.86f));
            bg.raycastTarget = false;

            TMP_Text pageLabel = AddText(canvasGo.transform, "PageLabel", "1 / 1 쪽", new Vector2(0.04f, 0.92f), new Vector2(0.96f, 0.985f), 30f, new Color(0.16f, 0.12f, 0.2f));

            // 칸: 위에서 아래, 왼쪽에서 오른쪽 순서 (BinderLayout의 칸 번호와 같음)
            var slots = new BinderSlotView[Columns * Rows];
            const float left = 0.04f, right = 0.96f, bottom = 0.11f, top = 0.91f, pad = 0.012f;
            float cellW = (right - left) / Columns, cellH = (top - bottom) / Rows;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                {
                    var min = new Vector2(left + c * cellW + pad, top - (r + 1) * cellH + pad);
                    var max = new Vector2(left + (c + 1) * cellW - pad, top - r * cellH - pad);
                    slots[r * Columns + c] = CreateSlot(canvasGo.transform, $"Slot_{r}_{c}", min, max);
                }

            Button prev = CreateUiButton(canvasGo.transform, "< 이전", new Vector2(0.04f, 0.02f), new Vector2(0.34f, 0.09f));
            Button next = CreateUiButton(canvasGo.transform, "다음 >", new Vector2(0.66f, 0.02f), new Vector2(0.96f, 0.09f));
            AddText(canvasGo.transform, "Hint", "카드를 누르면 선택", new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.09f), 20f, new Color(0.35f, 0.32f, 0.4f));

            var view = root.gameObject.AddComponent<BinderView>();
            var so = new SerializedObject(view);
            SerializedProperty slotsProp = so.FindProperty("slots");
            slotsProp.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++) slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            so.FindProperty("prevButton").objectReferenceValue = prev;
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("pageLabel").objectReferenceValue = pageLabel;
            so.FindProperty("styles").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CardRarityStyles>(StylesPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 칸 하나: 강조 테두리(뒤) → 틀 → 속성색 배경 → 캐릭터 그림 → 이름 · 장수
        static BinderSlotView CreateSlot(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, anchorMin, anchorMax);

            Image highlight = AddImage(go.transform, "Highlight", Vector2.zero, Vector2.one, new Color(1f, 0.85f, 0.25f, 0f));
            ((RectTransform)highlight.transform).offsetMin = new Vector2(-7f, -7f);
            ((RectTransform)highlight.transform).offsetMax = new Vector2(7f, 7f);
            highlight.raycastTarget = false;
            highlight.enabled = false;

            Image frame = AddImage(go.transform, "Frame", Vector2.zero, Vector2.one, Color.white); // 레이를 받는 유일한 그래픽

            Image artBg = AddImage(go.transform, "ArtBackground", new Vector2(0.07f, 0.24f), new Vector2(0.93f, 0.95f), Color.gray);
            artBg.raycastTarget = false;

            var artGo = new GameObject("Art", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            artGo.transform.SetParent(artBg.transform, false);
            var art = artGo.GetComponent<RawImage>();
            art.raycastTarget = false;
            var fitter = artGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; // 캐릭터 그림은 정사각형
            fitter.aspectRatio = 1f;

            TMP_Text nameText = AddText(go.transform, "Name", "", new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.23f), 22f, Color.black);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 12f;
            nameText.fontSizeMax = 22f;

            TMP_Text countText = AddText(go.transform, "Count", "", new Vector2(0.5f, 0.82f), new Vector2(0.97f, 0.97f), 22f, Color.white);
            countText.alignment = TextAlignmentOptions.TopRight;
            countText.fontStyle = FontStyles.Bold;

            var button = go.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1f, 0.8f, 0.55f, 1f),
                pressedColor = new Color(0.85f, 0.6f, 0.35f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.85f, 0.85f, 0.85f, 1f), // 못 얻은 칸: 살짝 어둡게
                colorMultiplier = 1f,
                fadeDuration = 0.05f,
            };
            go.AddComponent<UiHoverFeedback>();

            var slot = go.AddComponent<BinderSlotView>();
            slot.Configure(button, frame, artBg, art, nameText, countText, highlight);
            return slot;
        }

        // ------------------------------------------------------------------ 표지 (소켓 1개)

        static BinderCoverSocket CreateCover(Transform parent, Vector3 deskTopPos)
        {
            const float thickness = 0.03f;
            var root = new GameObject("BinderCover");
            root.transform.SetParent(parent, true);
            root.transform.position = deskTopPos;

            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = "Cover";
            cover.transform.SetParent(root.transform, false);
            cover.transform.localPosition = new Vector3(0f, thickness / 2f, 0f);
            cover.transform.localScale = new Vector3(0.24f, thickness, 0.32f);
            cover.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.18f, 0.22f, 0.42f) };

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, thickness + 0.002f, -0.1f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 표지 위에 누워 있는 글자
            var label = labelGo.AddComponent<TextMeshPro>();
            if (font != null) label.font = font;
            label.text = "카드를 여기에 놓으면\n바인더에 보관";
            label.fontSize = 0.3f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.9f, 0.6f);
            ((RectTransform)labelGo.transform).sizeDelta = new Vector2(0.22f, 0.06f);

            var socketGo = new GameObject("CoverSocket");
            socketGo.transform.SetParent(root.transform, false);
            socketGo.transform.localPosition = new Vector3(0f, thickness, 0.03f);

            var trigger = socketGo.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.05f, 0f);
            trigger.size = new Vector3(0.22f, 0.12f, 0.24f);

            // 카드 앞면(-Z)이 위를 보게 눕힘 (선반 칸의 faceUp과 같은 회전)
            var attach = new GameObject("Attach").transform;
            attach.SetParent(socketGo.transform, false);
            attach.localPosition = new Vector3(0f, 0.004f, 0f);
            attach.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var socket = socketGo.AddComponent<BinderCoverSocket>();
            socket.attachTransform = attach;
            socket.interactionLayers = CardInteractionLayers.Card; // 카드만 (팩은 Default 층이라 안 꽂힘)
            socket.showInteractableHoverMeshes = false;
            return socket;
        }

        // ------------------------------------------------------------------ 도우미

        // 집 블록아웃의 "책상 (바인더 · 준비 중)" 안내 글을 바꿉니다 (ShopSceneBuilder를 건드리지 않으려고 여기서 처리).
        static void RenameDeskLabel(Transform home)
        {
            foreach (TextMeshPro t in home.GetComponentsInChildren<TextMeshPro>(true))
            {
                if (!t.text.Contains("바인더 · 준비 중")) continue;
                t.text = "책상 · 바인더";
                t.transform.position += Vector3.up * 0.7f; // 세워 둔 바인더 페이지(높이 약 0.76m)에 가리지 않게 위로
            }
        }

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

        // ShopSceneBuilder.CreateUiButton과 같은 모양 (그쪽 함수는 private이라 같은 규칙으로 따로 둠)
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
