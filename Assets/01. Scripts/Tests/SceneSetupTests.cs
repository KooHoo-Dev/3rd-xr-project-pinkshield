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
        public void ShopScene_IsFirstBuildScene()
        {
            Assert.IsTrue(EditorBuildSettings.scenes.Length > 0);
            Assert.AreEqual(ScenePath, EditorBuildSettings.scenes[0].path);
        }
    }
}
