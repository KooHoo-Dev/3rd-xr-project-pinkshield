using CardShop.Cards;
using CardShop.Cards.EditorTools;
using CardShop.Core;
using CardShop.DebugTools;
using CardShop.EditorTools.Interior;
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

        const float CounterTop = ShopLayout.CounterTop;
        const float TableTop = ShopLayout.TableTop;
        // 팩 테이블(선반) 위치. 카운터 2.22m(x -1.11~1.11)와 겹치지 않게 (인테리어 설계 D8)
        static readonly Vector3 PackTablePos = ShopLayout.PackTablePos;

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

            // 인테리어 프리팹: 없을 때만 처음 배치로 만들고, 있으면 손으로 고친 그대로 씀 (D7)
            InteriorBuilder.EnsurePrefabs();
            ExternalArt.ApplyShadowDistance(10f);

            CreateLight();
            Transform env = CreateEnvironment();
            CreateXr();
            GameFlow flow = CreateGame();
            PackOpener opener = CreatePackSystem(flow);
            CreateShelf(env, opener);
            CreateCounterButtons(flow, opener);
            CustomerFloorBuilder.Create(flow, font);
            CreateDebugPanel(flow);
            CreatePauseMenu(flow);
            CreateHome(flow);
            BinderBuilder.Create();
            SalesPanelBuilder.Create();

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
            light.shadows = LightShadows.Hard; // Mobile_RPAsset은 소프트 그림자 미지원
            // 거리 쪽(+Z) 하늘에서 가게 안(-Z)으로: 앞 파노라마 창으로 햇빛이 들어와 카운터 앞까지 닿음 (인테리어 설계 5-1)
            go.transform.rotation = Quaternion.Euler(35f, 160f, 0f);

            // 천장이 해를 막으므로 실내는 대부분 환경광. 페이즈별 값은 PhaseLighting이 바꿈
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.6f, 0.58f);
        }

        // 가게 인테리어(LowPolyInterior) + 바깥 거리(PolygonCity) 프리팹. 프리팹이 없으면(에셋 미임포트) 예전 블록아웃.
        static Transform CreateEnvironment()
        {
            var root = new GameObject("Environment").transform;
            var shop = AssetDatabase.LoadAssetAtPath<GameObject>(InteriorBuilder.ShopPath);
            var street = AssetDatabase.LoadAssetAtPath<GameObject>(InteriorBuilder.StreetPath);
            if (street != null) ((GameObject)PrefabUtility.InstantiatePrefab(street)).transform.SetParent(root, false);
            if (shop != null)
            {
                ((GameObject)PrefabUtility.InstantiatePrefab(shop)).transform.SetParent(root, false);
                return root;
            }
            Debug.LogWarning("[ShopSceneBuilder] ShopInterior 프리팹이 없어 블록아웃으로 만듭니다 (LowPolyInterior 임포트 확인).");

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
                // 가게 · 인도 · 집 밖(도로 등)으로 못 나가게 + 혼잣말 (BUG-009)
                rig.AddComponent<PlayAreaGuard>().Configure(origin, font);
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
            revealer.Configure(AssetDatabase.LoadAssetAtPath<CardView>(CardPrefabPath), anchor,
                AssetDatabase.LoadAssetAtPath<RarityRevealProfile>(DataPaths.RevealProfile));

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
            UnityEventTools.AddPersistentListener(clockOut.pressed, opener.TryClockOut);
        }

        // 카운터 위에 놓인 누름 버튼: 받침 + 위로 튀어나온 버튼 + 글자 (CustomerFloorBuilder의 결제 버튼도 사용)
        internal static PokeButton CreatePokeButton(string name, Vector3 position, string label, Color color)
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

        // 월드 UI 버튼 규격 (캔버스 높이 대비 비율, 1000px = 1m)
        const float ButtonHeight = 56f / 1100f;
        const float ButtonStep = 76f / 1100f;

        static void CreateDebugPanel(GameFlow flow)
        {
            var canvasGo = new GameObject("PhaseDebugPanel", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(500f, 1100f);
            rt.localScale = Vector3.one * 0.001f;               // 0.5m x 1.1m (1px = 1mm)
            // 패널 높이 1.1m. 중심 1.3이면 아래 끝(0.75)이 카운터(윗면 0.9) 속에 묻혀 마지막 버튼이 가려졌음 → 아래 끝 0.95
            rt.position = new Vector3(0.95f, 1.5f, 1.0f);
            rt.rotation = Quaternion.Euler(0f, 35f, 0f);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)bg.transform, Vector2.zero, Vector2.one);
            bg.GetComponent<Image>().color = new Color(0.1f, 0.08f, 0.14f, 0.85f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(canvasGo.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            Stretch(labelRt, new Vector2(0f, 0.636f), Vector2.one);
            labelRt.offsetMin = new Vector2(24f, 0f);
            labelRt.offsetMax = new Vector2(-24f, -20f);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "Phase";
            label.fontSize = 34f;
            label.alignment = TextAlignmentOptions.TopLeft;
            if (font != null) label.font = font;

            var cheats = canvasGo.AddComponent<DebugCheats>();
            cheats.Configure(flow, new GameObject[0]);

            // 버튼: 위에서부터 한 줄씩. 높이 56mm, 간격 20mm (시연 피드백 C2: 6mm였을 때 옆 버튼이 눌림)
            string[] names = { "다음 (Advance)", "퇴근 전으로 건너뛰기", "재화 +100", "다음 팩: 갓팩", "다음 팩: 레전더리 확정", "다음 팩: 유니크 히트", "눈높이 맞추기 (1.6m)", "일시정지 (테스트)" };
            UnityAction[] actions = { null, cheats.SkipToAfterHours, cheats.AddMoney100, cheats.ForceGodPack, cheats.ForceLegendaryPack, cheats.ForceUniqueHit, cheats.CalibrateHeight, cheats.PauseNow };
            Button advance = null;
            for (int i = 0; i < names.Length; i++)
            {
                float top = 0.62f - i * ButtonStep;
                Button b = CreateUiButton(canvasGo.transform, names[i], new Vector2(0.08f, top - ButtonHeight), new Vector2(0.92f, top));
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

        // ------------------------------------------------------------------ 집 구역 (시연 피드백: 퇴근 후 쉬는 느낌)

        // 가게에서 멀리 떨어진 같은 씬의 다른 구역. 모델은 HomeInterior 프리팹(LowPolyInterior), 여기서는 게임 기능만 붙임.
        static readonly Vector3 HomeOrigin = ShopLayout.HomeOrigin;
        public const string HomeRootName = "Home";

        static void CreateHome(GameFlow flow)
        {
            var root = new GameObject(HomeRootName).transform;
            root.position = HomeOrigin;

            var interior = AssetDatabase.LoadAssetAtPath<GameObject>(InteriorBuilder.HomePath);
            if (interior != null) ((GameObject)PrefabUtility.InstantiatePrefab(interior)).transform.SetParent(root, false);
            else
            {
                Debug.LogWarning("[ShopSceneBuilder] HomeInterior 프리팹이 없어 집을 블록아웃으로 만듭니다.");
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Floor";
                floor.transform.SetParent(root, false);
                floor.transform.localScale = new Vector3(0.5f, 1f, 0.5f); // 5m x 5m
                floor.GetComponent<MeshRenderer>().sharedMaterial = TintedMaterial(new Color(0.55f, 0.42f, 0.32f));
            }

            // 책상(바인더 자리): BinderBuilder가 이 상자의 위치 · 크기로 바인더를 올림. 모양은 인테리어의 OfficeTable(윗면 0.8)이라 상자는 숨김(충돌만).
            HomeCube(root, "Desk", new Vector3(-1.2f, 0.4f, 1.6f), new Vector3(1.2f, 0.8f, 0.6f), new Color(0.6f, 0.45f, 0.3f));
            if (interior != null) root.Find("Desk").GetComponent<MeshRenderer>().enabled = false;
            // 커뮤니티 모니터 (벽걸이 큰 화면). CommunityScreenBuilder가 크기 · 위치를 정함
            HomeCube(root, "Monitor", new Vector3(1.2f, 1.05f, 1.8f), new Vector3(0.7f, 0.4f, 0.04f), new Color(0.1f, 0.15f, 0.25f));
            CommunityScreenBuilder.Create(root, font); // 카드 커뮤니티 화면 (모니터 확대 + 월드 텍스트)

            // 잠자기 = 침대 옆 벽 전등 스위치 (H1: "불 끄고 잠자기"). 누르는 면이 방 안(+Z)을 봄
            Vector3 switchPos = HomeOrigin + ShopLayout.HomeSleepSwitch + new Vector3(0f, 0f, 0.012f);
            PokeButton sleep = CreatePokeButton("Button_Sleep", switchPos, "불 끄고 자기", new Color(0.55f, 0.6f, 1f));
            sleep.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Transform sleepLabel = sleep.transform.Find("Label");
            if (sleepLabel != null) sleepLabel.SetPositionAndRotation(switchPos + new Vector3(0f, 0.12f, 0.01f), Quaternion.Euler(0f, 180f, 0f));
            sleep.transform.SetParent(root, true);
            UnityEventTools.AddPersistentListener(sleep.pressed, flow.Sleep);

            // 집에서만 켜지는 따뜻한 조명
            var lightGo = new GameObject("HomeLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.2f, 0.8f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.82f, 0.6f);
            light.intensity = 3f;
            light.range = 7f;
            lightGo.SetActive(false);

            var spawn = new GameObject("HomeSpawn").transform;
            spawn.SetParent(root, false);
            spawn.localPosition = new Vector3(0f, 0f, -0.8f);

            // 페이드 · 이동
            XROrigin origin = Object.FindFirstObjectByType<XROrigin>();
            var transitionGo = new GameObject("DayTransition");
            var transition = transitionGo.AddComponent<DayTransition>();
            var so = new SerializedObject(transition);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("origin").objectReferenceValue = origin;
            so.FindProperty("homeSpawn").objectReferenceValue = spawn;
            so.FindProperty("font").objectReferenceValue = font;
            so.FindProperty("morningLines").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MorningLineSet>(DataPaths.MorningLines);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 페이즈별 조명 분위기
            var lighting = transitionGo.AddComponent<PhaseLighting>();
            var lso = new SerializedObject(lighting);
            lso.FindProperty("sun").objectReferenceValue = Object.FindFirstObjectByType<Light>();
            lso.FindProperty("homeLight").objectReferenceValue = lightGo;
            lso.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material TintedMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            return mat;
        }

        static void HomeCube(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = TintedMaterial(color);
        }

        // ------------------------------------------------------------------ 일시정지 메뉴 (시연 피드백 C1)

        static void CreatePauseMenu(GameFlow flow)
        {
            var system = new GameObject("PauseSystem");

            var canvasGo = new GameObject("PauseMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            canvasGo.transform.SetParent(system.transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(500f, 560f);           // 1px = 1mm
            rt.localScale = Vector3.one * 0.001f;
            rt.position = new Vector3(0f, 1.4f, 1.0f);        // 실제 위치는 PauseMenu가 눈앞으로 옮김

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)bg.transform, Vector2.zero, Vector2.one);
            bg.GetComponent<Image>().color = new Color(0.1f, 0.08f, 0.14f, 0.92f);

            CreateLabel(canvasGo.transform, "Title", "일시정지", new Vector2(0f, 0.7f), new Vector2(1f, 0.95f), 44f);

            GameObject main = CreatePanel(canvasGo.transform, "MainPanel");
            Button resume = CreateUiButton(main.transform, "이어서 하기", new Vector2(0.08f, 0.44f), new Vector2(0.92f, 0.54f));
            Button restart = CreateUiButton(main.transform, "처음부터 다시 시작", new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.38f));

            GameObject confirm = CreatePanel(canvasGo.transform, "ConfirmPanel");
            CreateLabel(confirm.transform, "Message", "정말 처음부터 다시 시작할까요?\n진행 상황이 사라져요.", new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.7f), 28f);
            Button yes = CreateUiButton(confirm.transform, "예, 처음부터", new Vector2(0.08f, 0.27f), new Vector2(0.92f, 0.37f));
            Button no = CreateUiButton(confirm.transform, "아니요", new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.22f));

            TMP_Text countdown = CreateLabel(canvasGo.transform, "Countdown", "3", new Vector2(0f, 0.2f), new Vector2(1f, 0.7f), 160f);

            var menu = system.AddComponent<PauseMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("root").objectReferenceValue = canvasGo;
            so.FindProperty("mainPanel").objectReferenceValue = main;
            so.FindProperty("confirmPanel").objectReferenceValue = confirm;
            so.FindProperty("countdownLabel").objectReferenceValue = countdown;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("restartButton").objectReferenceValue = restart;
            so.FindProperty("confirmYesButton").objectReferenceValue = yes;
            so.FindProperty("confirmNoButton").objectReferenceValue = no;
            so.ApplyModifiedPropertiesWithoutUndo();

            canvasGo.SetActive(false);
        }

        static GameObject CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, Vector2.zero, Vector2.one);
            return go;
        }

        static TMP_Text CreateLabel(Transform parent, string name, string text, Vector2 min, Vector2 max, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, min, max);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            if (font != null) label.font = font;
            return label;
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
            label.color = Color.white;
            if (font != null) label.font = font;

            // 레이가 닿으면 색이 확 바뀌게 (시연 피드백 C3: 기본 하이라이트는 거의 흰색이라 안 보였음)
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
