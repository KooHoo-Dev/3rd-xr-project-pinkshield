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
            Assert.IsNotNull(new SerializedObject(revealer).FindProperty("cardPrefab").objectReferenceValue, "카드 프리팹 미연결");

            CardShop.Interaction.PokeButton[] buttons = Object.FindObjectsByType<CardShop.Interaction.PokeButton>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(buttons.Length, 2, "상자·퇴근 버튼");
            foreach (var b in buttons)
                Assert.Greater(b.pressed.GetPersistentEventCount(), 0, $"{b.name}: 연결된 동작 없음");
        }

        [Test]
        public void ShopScene_IsFirstBuildScene()
        {
            Assert.IsTrue(EditorBuildSettings.scenes.Length > 0);
            Assert.AreEqual(ScenePath, EditorBuildSettings.scenes[0].path);
        }
    }
}
