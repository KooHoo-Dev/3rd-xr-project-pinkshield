using CardShop.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>Shop 씬 연결 상태 검사. 씬을 손으로 고치다 참조가 빠지면 여기서 잡힙니다.</summary>
    public class SceneSetupTests
    {
        const string ScenePath = "Assets/00. Scenes/Shop.unity";

        [Test]
        public void ShopScene_HasOneGameContextWithAllConfigs()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameContext[] contexts = Object.FindObjectsByType<GameContext>(FindObjectsSortMode.None);
            Assert.AreEqual(1, contexts.Length, "GameContext는 씬에 하나");

            var so = new SerializedObject(contexts[0]);
            foreach (string field in new[] { "cardDatabase", "rarityTable", "packOdds", "economy", "campaign", "score" })
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, $"GameContext.{field} 미연결");

            Assert.IsNotNull(contexts[0].GetComponent<GameFlow>(), "GameFlow 없음");
        }

        [Test]
        public void ShopScene_HasShelfPackSystemAndButtons()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var shelf = Object.FindAnyObjectByType<CardShop.Interaction.ShelfController>();
            Assert.IsNotNull(shelf, "팩 선반 없음");
            var so = new SerializedObject(shelf);
            Assert.IsNotNull(so.FindProperty("packPrefab").objectReferenceValue, "선반 팩 프리팹 미연결");
            Assert.IsNotNull(so.FindProperty("opener").objectReferenceValue, "선반 PackOpener 미연결");
            Assert.Greater(so.FindProperty("slots").arraySize, 0, "선반 칸 없음");

            var revealer = Object.FindAnyObjectByType<CardShop.Presentation.CardRevealer>();
            Assert.IsNotNull(revealer, "CardRevealer 없음");
            var revealerSo = new SerializedObject(revealer);
            Assert.IsNotNull(revealerSo.FindProperty("cardPrefab").objectReferenceValue, "카드 프리팹 미연결");
            Assert.IsNotNull(revealerSo.FindProperty("revealProfile").objectReferenceValue, "등급별 연출 프로필 미연결");

            CardShop.Interaction.PokeButton[] buttons = Object.FindObjectsByType<CardShop.Interaction.PokeButton>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(buttons.Length, 2, "상자·퇴근 버튼");
            foreach (var b in buttons)
                Assert.Greater(b.pressed.GetPersistentEventCount(), 0, $"{b.name}: 연결된 동작 없음");
        }

        // 퇴근 후 집: 전환 연출 · 집 구역 · 침대 버튼이 연결돼 있어야 하루가 이어짐
        [Test]
        public void ShopScene_HasHomeTransitionAndSleepButton()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var transition = Object.FindAnyObjectByType<CardShop.Presentation.DayTransition>();
            Assert.IsNotNull(transition, "DayTransition 없음 (Build Shop Scene 다시 실행)");
            var so = new SerializedObject(transition);
            foreach (string field in new[] { "flow", "origin", "homeSpawn" })
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, $"DayTransition.{field} 미연결");

            var sleep = GameObject.Find("Button_Sleep");
            Assert.IsNotNull(sleep, "잠자기 버튼 없음");
            Assert.Greater(sleep.GetComponent<CardShop.Interaction.PokeButton>().pressed.GetPersistentEventCount(), 0, "잠자기 버튼에 연결된 동작 없음");
        }

        // 시연 피드백 C1: 일시정지 메뉴가 연결돼 있어야 헤드셋을 벗었다 써도 영업을 이어갈 수 있음
        [Test]
        public void ShopScene_HasPauseMenuWithAllReferences()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var menu = Object.FindAnyObjectByType<CardShop.UI.PauseMenu>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "PauseMenu 없음 (Build Shop Scene 다시 실행)");
            var so = new SerializedObject(menu);
            foreach (string field in new[] { "flow", "root", "mainPanel", "confirmPanel", "countdownLabel", "resumeButton", "restartButton", "confirmYesButton", "confirmNoButton" })
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, $"PauseMenu.{field} 미연결");
        }

        // 집 컴퓨터의 카드 커뮤니티 화면이 연결돼 있어야 인기 속성 예보가 보임
        [Test]
        public void ShopScene_HasCommunityScreenWithAllReferences()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var screen = Object.FindAnyObjectByType<CardShop.UI.CommunityScreen>(FindObjectsInactive.Include);
            Assert.IsNotNull(screen, "CommunityScreen 없음 (Build Shop Scene 다시 실행)");
            var so = new SerializedObject(screen);
            foreach (string field in new[] { "content", "forecastLabel", "topLabel", "moodLabel" })
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, $"CommunityScreen.{field} 미연결");
        }

        // 바인더: 칸 · 페이지 버튼 · 등급 색 · 표지 소켓이 연결돼 있고, 표지 소켓과 카드가 같은 Interaction Layer(Card)를 써야 카드가 꽂힘
        [Test]
        public void ShopScene_HasBinderWithSlotsButtonsAndCoverSocket()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var binder = Object.FindAnyObjectByType<CardShop.Presentation.BinderView>();
            Assert.IsNotNull(binder, "BinderView 없음 (Build Shop Scene 다시 실행)");
            var so = new SerializedObject(binder);
            SerializedProperty slots = so.FindProperty("slots");
            Assert.Greater(slots.arraySize, 0, "바인더 칸 없음");
            for (int i = 0; i < slots.arraySize; i++)
                Assert.IsNotNull(slots.GetArrayElementAtIndex(i).objectReferenceValue, $"바인더 칸 {i} 미연결");
            foreach (string field in new[] { "prevButton", "nextButton", "pageLabel", "styles", "coverSocket" })
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, $"BinderView.{field} 미연결");

            const long cardLayer = 1L << 3; // CardInteractionLayers.CardIndex
            var socket = new SerializedObject(so.FindProperty("coverSocket").objectReferenceValue);
            Assert.AreEqual(cardLayer, socket.FindProperty("m_InteractionLayers.m_Bits").longValue, "표지 소켓은 Card 층만");

            var card = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03. Prefabs/Cards/Card.prefab");
            Assume.That(card, Is.Not.Null, "카드 프리팹 없음");
            var grab = new SerializedObject(card.GetComponent("XRGrabInteractable"));
            Assert.AreNotEqual(0L, grab.FindProperty("m_InteractionLayers.m_Bits").longValue & cardLayer, "카드가 Card 층에 없어 바인더에 안 꽂힘 (Build Card Prefab 다시 실행)");
        }

        // 판매 패널: 바인더와 버튼 · 글자 · 진열 목록 버튼이 연결돼 있어야 카드를 골라 진열 · 즉시 판매할 수 있음
        [Test]
        public void ShopScene_HasSalesPanelWithAllReferences()
        {
            Assume.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Shop 씬 없음");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var panel = Object.FindAnyObjectByType<CardShop.UI.SalesPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "SalesPanel 없음 (Build Shop Scene 다시 실행)");
            var so = new SerializedObject(panel);
            foreach (string field in new[] { "content", "binder", "selectedLabel", "statusLabel", "messageLabel", "displayButton", "sellButton" })
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, $"SalesPanel.{field} 미연결");
            SerializedProperty list = so.FindProperty("displayedButtons");
            Assert.Greater(list.arraySize, 0, "진열 목록 버튼 없음");
            for (int i = 0; i < list.arraySize; i++)
                Assert.IsNotNull(list.GetArrayElementAtIndex(i).objectReferenceValue, $"진열 목록 버튼 {i} 미연결");
        }

        // BUG-001: 스크립트가 빠진 컴포넌트는 에러 없이 조용히 동작을 멈춥니다.
        [Test]
        public void PackPrefab_HasNoMissingScriptsAndOpenMethod()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03. Prefabs/Packs/Pack.prefab");
            Assume.That(prefab, Is.Not.Null, "팩 프리팹 없음");
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), $"{t.name}: 스크립트 없는 컴포넌트");
            Assert.IsNotNull(prefab.GetComponent<CardShop.Interaction.IPackOpenMethod>(), "개봉 방식(IPackOpenMethod) 없음");
        }

        // 시연 피드백 A4: 뜯김 연출이 윗 압착 띠 조각을 이름으로 찾습니다. 이름이 바뀌면 띠가 날아가지 않고 팩에 남습니다.
        [Test]
        public void PackPrefab_HasTearEffectAndTopStripParts()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03. Prefabs/Packs/Pack.prefab");
            Assume.That(prefab, Is.Not.Null, "팩 프리팹 없음");
            Assert.IsNotNull(prefab.GetComponent<CardShop.Presentation.PackTearEffect>(), "PackTearEffect 없음 (Build Pack Prefab 다시 실행)");
            Transform visual = prefab.transform.Find("Visual");
            Assert.IsNotNull(visual, "Visual 없음");
            Assert.IsNotNull(visual.Find("SealTop"), "윗 압착 띠(SealTop) 없음");
            Assert.IsNotNull(visual.Find("FoldHinge"), "접힘 모서리(FoldHinge) 없음");
        }

        [Test]
        public void ShopScene_IsFirstBuildScene()
        {
            Assert.IsTrue(EditorBuildSettings.scenes.Length > 0);
            Assert.AreEqual(ScenePath, EditorBuildSettings.scenes[0].path);
        }
    }
}
