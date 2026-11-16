using CardShop.Cards;
using CardShop.Cards.EditorTools;
using CardShop.Core;
using CardShop.DebugTools;
using CardShop.Interaction;
using CardShop.Presentation;
using CardShop.UI;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace CardShop.EditorTools
{
    /// <summary>
    /// Shop 씬(게임 유일 씬)의 골격을 만듭니다. 메뉴: Tools > CardShop > Build Shop Scene
    /// 카드·팩 프리팹도 함께 다시 만듭니다. 배치(blockout)·소품은 만든 뒤 씬에서 직접 작업하세요.
    ///
    /// 배치 기준: 플레이어는 원점에서 +Z를 봄. 카운터는 앞(z≈0.7), 팩 선반 테이블은 왼쪽.
    /// </summary>
    public static class ShopSceneBuilder
    {
        public const string ScenePath = "Assets/00. Scenes/Shop.unity";
        const string XrOriginPrefab = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string CardPrefabPath = "Assets/03. Prefabs/Cards/Card.prefab";
        const string FontPath = "Assets/99. External Assets/Font/Noto_Sans_KR/NotoSansKR-Regular SDF.asset";

        const float CounterTop = 0.9f;
        const float TableTop = 0.8f;
        // 팩 테이블(선반) 위치. 카운터(x -0.7~0.7, z 0.4~1.0)와 겹치지 않게 씬에서 조정한 값.
        static readonly Vector3 PackTablePos = new Vector3(-1.356f, 0f, 0.65f);

        static TMP_FontAsset font;

        [MenuItem("Tools/CardShop/Build Shop Scene", priority = 30)]
        static void BuildFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Build Shop Scene", $"{ScenePath} 를 새로 만들까요? 씬에서 한 작업이 사라집니다.", "새로 만들기", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        /// <summary>배치 모드(-executeMethod)에서도 호출 가능. 확인 창 없이 덮어씁니다.</summary>
        public static void Build()
        {
            ConfigAssetCreator.EnsureAll();
            CardPrefabBuilder.Build();
            PackPrefabBuilder.Build();
            DataPaths.EnsureFolder("Assets/00. Scenes");
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight();
            Transform env = CreateBlockout();
            CreateXr();
            GameFlow flow = CreateGame();
            PackOpener opener = CreatePackSystem(flow);
            CreateShelf(env, opener);
            CreateCounterButtons(flow, opener);
            CreateDebugPanel(flow);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShopSceneBuilder] {ScenePath} 생성 완료");
        }

        // ------------------------------------------------------------------ 환경

        static void CreateLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // 임시 블록아웃: 바닥 + 카운터 + 팩 테이블. 맵 에셋으로 교체 예정.
        static Transform CreateBlockout()
        {
            var root = new GameObject("Environment (Blockout)").transform;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(root);
            floor.transform.localScale = new Vector3(0.6f, 1f, 0.6f); // 6m x 6m

            Cube(root, "Counter", new Vector3(0f, CounterTop / 2f, 0.7f), new Vector3(1.4f, CounterTop, 0.6f));
            Cube(root, "PackTable", PackTablePos + Vector3.up * (TableTop / 2f), new Vector3(0.6f, TableTop, 0.5f));
            return root;
        }

        static GameObject Cube(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = scale;
            return go;
        }

        static void CreateXr()
        {
            new GameObject("XR Interaction Manager", typeof(XRInteractionManager));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefab);
            if (prefab != null)
            {
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var origin = rig.GetComponent<XROrigin>();
                // 바닥 기준 트래킹 + 시작 시 눈높이 보정 (지정 안 함이면 기기에 따라 시점이 낮아짐)
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
                rig.AddComponent<PlayerHeightCalibrator>().Configure(origin);
            }
            else Debug.LogError($"[ShopSceneBuilder] XR Origin 프리팹 없음: {XrOriginPrefab} (XRI Starter Assets 샘플 임포트 필요)");

            new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
        }

        // ------------------------------------------------------------------ 게임

        static GameFlow CreateGame()
        {
            var go = new GameObject("Game");
            var context = go.AddComponent<GameContext>();
            var flow = go.AddComponent<GameFlow>();

            var so = new SerializedObject(context);
            so.FindProperty("cardDatabase").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CardDatabase>(DataPaths.CardDatabase);
            so.FindProperty("rarityTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RarityTable>(DataPaths.RarityTable);
            so.FindProperty("packOdds").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PackOddsConfig>(DataPaths.PackOdds);
            so.FindProperty("economy").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EconomyConfig>(DataPaths.Economy);
            so.FindProperty("campaign").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CampaignConfig>(DataPaths.Campaign);
            so.FindProperty("score").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScoreConfig>(DataPaths.Score);
            so.ApplyModifiedPropertiesWithoutUndo();
            return flow;
        }

        static PackOpener CreatePackSystem(GameFlow flow)
        {
            var go = new GameObject("PackSystem");

            // 카드가 펼쳐지는 자리: 카운터 위, 앞면(-Z)이 플레이어 쪽, 살짝 위로 기울임
            var anchor = new GameObject("RevealAnchor").transform;
            anchor.SetParent(go.transform);
            anchor.SetPositionAndRotation(new Vector3(0f, CounterTop + 0.2f, 0.5f), Quaternion.Euler(25f, 0f, 0f));

            var revealer = go.AddComponent<CardRevealer>();
            revealer.Configure(AssetDatabase.LoadAssetAtPath<CardView>(CardPrefabPath), anchor);

            var opener = go.AddComponent<PackOpener>();
            opener.Configure(flow, revealer);
            return opener;
        }

        // 팩 테이블 위 칸들(4열). 팩은 눕혀 놓이고 윗변이 +Z(플레이어 반대쪽)를 향합니다.
        static void CreateShelf(Transform env, PackOpener opener)
        {
            // 칸 수는 CampaignConfig.shelfSlotCount (기본 12 = 4 x 3)
            int count = AssetDatabase.LoadAssetAtPath<CampaignConfig>(DataPaths.Campaign).shelfSlotCount;
            const int cols = 4;
            int rows = Mathf.CeilToInt(count / (float)cols);
            const float dx = 0.13f, dz = 0.15f;

            var shelfGo = new GameObject("PackShelf");
            shelfGo.transform.position = PackTablePos + Vector3.up * TableTop;

            Quaternion faceUp = Quaternion.Euler(90f, 0f, 0f);                     // 앞면(-Z)이 위
            Quaternion faceDown = faceUp * Quaternion.Euler(0f, 180f, 0f);         // 긴 축으로 뒤집음 (윗변 방향 유지)

            var slots = new ShelfSlotSocket[count];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols && r * cols + c < count; c++)
                {
                    var slotGo = new GameObject($"Slot_{r}_{c}");
                    slotGo.transform.SetParent(shelfGo.transform, false);
                    slotGo.transform.localPosition = new Vector3((c - (cols - 1) / 2f) * dx, 0.006f, (r - (rows - 1) / 2f) * dz);

                    var trigger = slotGo.AddComponent<BoxCollider>();
                    trigger.isTrigger = true;
                    trigger.size = new Vector3(0.1f, 0.08f, 0.14f);

                    Transform up = Child(slotGo.transform, "AttachFaceUp", faceUp);
                    Transform down = Child(slotGo.transform, "AttachFaceDown", faceDown);

                    var socket = slotGo.AddComponent<ShelfSlotSocket>();
                    socket.Configure(up, down);
                    socket.showInteractableHoverMeshes = false;
                    slots[r * cols + c] = socket;
                }

            var shelf = shelfGo.AddComponent<ShelfController>();
            shelf.Configure(slots, AssetDatabase.LoadAssetAtPath<PackView>(PackPrefabBuilder.PrefabPath), opener);
        }

        static Transform Child(Transform parent, string name, Quaternion localRot)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localRotation = localRot;
            return t;
        }

        static void CreateCounterButtons(GameFlow flow, PackOpener opener)
        {
            var economy = AssetDatabase.LoadAssetAtPath<EconomyConfig>(DataPaths.Economy);
            PokeButton box = CreatePokeButton("Button_OpenBox", new Vector3(0.35f, CounterTop, 0.5f), $"상자 열기 ({economy.boxPrice})", new Color(1f, 0.75f, 0.3f));
            UnityEventTools.AddPersistentListener(box.pressed, opener.TryOpenBox);

            PokeButton clockOut = CreatePokeButton("Button_ClockOut", new Vector3(0.55f, CounterTop, 0.5f), "퇴근", new Color(0.45f, 0.8f, 1f));
            UnityEventTools.AddPersistentListener(clockOut.pressed, flow.ClockOut);
        }

        // 카운터 위에 놓인 누름 버튼: 받침 + 위로 튀어나온 버튼 + 글자
        static PokeButton CreatePokeButton(string name, Vector3 position, string label, Color color)
        {
            var root = new GameObject(name);
            root.transform.position = position;

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = "Cap";
            cap.transform.SetParent(root.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            cap.transform.localScale = new Vector3(0.07f, 0.02f, 0.07f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            cap.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(cap.GetComponent<Collider>());

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.015f, 0f);
            col.size = new Vector3(0.07f, 0.03f, 0.07f);

            var simple = root.AddComponent<XRSimpleInteractable>();
            var poke = root.AddComponent<XRPokeFilter>();
            poke.pokeInteractable = simple;
            poke.pokeCollider = col;
            poke.pokeConfiguration = new PokeThresholdDatumProperty(new PokeThresholdData { pokeDirection = PokeAxis.None });

            var button = root.AddComponent<PokeButton>();
            var so = new SerializedObject(button);
            so.FindProperty("pressVisual").objectReferenceValue = cap.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(root.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            textGo.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            var text = textGo.AddComponent<TextMeshPro>();
            if (font != null) text.font = font;
            text.text = label;
            text.fontSize = 0.25f;
            text.alignment = TextAlignmentOptions.Center;
            ((RectTransform)textGo.transform).sizeDelta = new Vector2(0.18f, 0.05f);
            return button;
        }

        // ------------------------------------------------------------------ 디버그 패널

        static void CreateDebugPanel(GameFlow flow)
        {
            var canvasGo = new GameObject("PhaseDebugPanel", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(500f, 760f);
            rt.localScale = Vector3.one * 0.001f;               // 0.5m x 0.76m
            rt.position = new Vector3(0.95f, 1.35f, 1.0f);
            rt.rotation = Quaternion.Euler(0f, 35f, 0f);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)bg.transform, Vector2.zero, Vector2.one);
            bg.GetComponent<Image>().color = new Color(0.1f, 0.08f, 0.14f, 0.85f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(canvasGo.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            Stretch(labelRt, new Vector2(0f, 0.52f), Vector2.one);
            labelRt.offsetMin = new Vector2(24f, 0f);
            labelRt.offsetMax = new Vector2(-24f, -20f);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "Phase";
            label.fontSize = 34f;
            label.alignment = TextAlignmentOptions.TopLeft;
            if (font != null) label.font = font;

            var cheats = canvasGo.AddComponent<DebugCheats>();
            cheats.Configure(flow, new GameObject[0]);

            // 버튼: 위에서부터 한 줄씩
            string[] names = { "다음 (Advance)", "퇴근 전으로 건너뛰기", "재화 +100", "다음 팩: 갓팩", "다음 팩: 레전더리 확정", "다음 팩: 유니크 히트", "눈높이 맞추기 (1.6m)" };
            UnityAction[] actions = { null, cheats.SkipToAfterHours, cheats.AddMoney100, cheats.ForceGodPack, cheats.ForceLegendaryPack, cheats.ForceUniqueHit, cheats.CalibrateHeight };
            Button advance = null;
            for (int i = 0; i < names.Length; i++)
            {
                float top = 0.52f - i * 0.072f;
                Button b = CreateUiButton(canvasGo.transform, names[i], new Vector2(0.08f, top - 0.064f), new Vector2(0.92f, top));
                if (actions[i] != null) UnityEventTools.AddPersistentListener(b.onClick, actions[i]);
                else advance = b;
            }

            var panel = canvasGo.AddComponent<PhaseDebugPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("advanceButton").objectReferenceValue = advance;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Button CreateUiButton(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            go.name = "Button_" + text;
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, anchorMin, anchorMax);
            var label = go.GetComponentInChildren<TMP_Text>();
            label.text = text;
            label.fontSize = 28f;
            if (font != null) label.font = font;
            return go.GetComponent<Button>();
        }

        static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
