using CardShop.Cards;
using CardShop.Cards.EditorTools;
using CardShop.Core;
using CardShop.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace CardShop.EditorTools
{
    /// <summary>
    /// Shop 씬(게임 유일 씬)의 기본 골격을 만듭니다. 메뉴: Tools > CardShop > Build Shop Scene
    /// 이미 씬이 있으면 덮어쓸지 묻습니다. 배치(blockout)·소품은 만든 뒤 씬에서 직접 작업하세요.
    /// </summary>
    public static class ShopSceneBuilder
    {
        public const string ScenePath = "Assets/00. Scenes/Shop.unity";
        const string XrOriginPrefab = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string FontPath = "Assets/99. External Assets/Font/Noto_Sans_KR/NotoSansKR-Regular SDF.asset";

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
            DataPaths.EnsureFolder("Assets/00. Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight();
            CreateBlockout();
            CreateXr();
            GameFlow flow = CreateGame();
            CreateDebugPanel(flow);

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShopSceneBuilder] {ScenePath} 생성 완료");
        }

        static void CreateLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // 임시 블록아웃: 바닥 + 카운터. 맵 에셋으로 교체 예정.
        static void CreateBlockout()
        {
            var root = new GameObject("Environment (Blockout)");

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform);
            floor.transform.localScale = new Vector3(0.6f, 1f, 0.6f); // 6m x 6m

            GameObject counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            counter.name = "Counter";
            counter.transform.SetParent(root.transform);
            counter.transform.position = new Vector3(0f, 0.45f, 0.7f);
            counter.transform.localScale = new Vector3(1.4f, 0.9f, 0.6f);
        }

        static void CreateXr()
        {
            new GameObject("XR Interaction Manager", typeof(XRInteractionManager));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefab);
            if (prefab != null) PrefabUtility.InstantiatePrefab(prefab);
            else Debug.LogError($"[ShopSceneBuilder] XR Origin 프리팹 없음: {XrOriginPrefab} (XRI Starter Assets 샘플 임포트 필요)");

            new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
        }

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

        static void CreateDebugPanel(GameFlow flow)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var canvasGo = new GameObject("PhaseDebugPanel", typeof(RectTransform), typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(500f, 400f);
            rt.localScale = Vector3.one * 0.001f;               // 0.5m x 0.4m
            rt.position = new Vector3(0.9f, 1.4f, 1.1f);
            rt.rotation = Quaternion.Euler(0f, 30f, 0f);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)bg.transform);
            bg.GetComponent<Image>().color = new Color(0.1f, 0.08f, 0.14f, 0.85f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(canvasGo.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = new Vector2(0f, 0.3f);
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(24f, 0f);
            labelRt.offsetMax = new Vector2(-24f, -24f);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "Phase";
            label.fontSize = 40f;
            label.alignment = TextAlignmentOptions.TopLeft;
            if (font != null) label.font = font;

            GameObject buttonGo = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            buttonGo.name = "AdvanceButton";
            buttonGo.transform.SetParent(canvasGo.transform, false);
            var buttonRt = (RectTransform)buttonGo.transform;
            buttonRt.anchorMin = new Vector2(0.1f, 0.05f);
            buttonRt.anchorMax = new Vector2(0.9f, 0.25f);
            buttonRt.offsetMin = buttonRt.offsetMax = Vector2.zero;
            var buttonText = buttonGo.GetComponentInChildren<TMP_Text>();
            buttonText.text = "다음 (Advance)";
            buttonText.fontSize = 36f;
            if (font != null) buttonText.font = font;

            var panel = canvasGo.AddComponent<PhaseDebugPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("advanceButton").objectReferenceValue = buttonGo.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void SetBuildScenes()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
