using UnityEditor;
using UnityEngine;

namespace CardShop.Cards.EditorTools
{
    /// <summary>
    /// 설정 SO가 없으면 기획서 기본값으로 만듭니다. 이미 있으면 건드리지 않습니다.
    /// 메뉴: Tools > CardShop > Create Missing Configs
    /// </summary>
    public static class ConfigAssetCreator
    {
        [MenuItem("Tools/CardShop/Create Missing Configs", priority = 11)]
        public static void EnsureAll()
        {
            DataPaths.EnsureFolder(DataPaths.ConfigsDir);
            Ensure<CardDatabase>(DataPaths.CardDatabase);
            Ensure<RarityTable>(DataPaths.RarityTable);
            Ensure<PackOddsConfig>(DataPaths.PackOdds);
            Ensure<EconomyConfig>(DataPaths.Economy);
            Ensure<CampaignConfig>(DataPaths.Campaign);
            Ensure<ScoreConfig>(DataPaths.Score);
            Ensure<RarityRevealProfile>(DataPaths.RevealProfile);
            Ensure<MorningLineSet>(DataPaths.MorningLines);
            Ensure<ShopItemConfig>(DataPaths.ShopItems);
            Ensure<CustomerLineSet>(DataPaths.CustomerLines);
            AssetDatabase.SaveAssets();
        }

        static void Ensure<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
            Debug.Log($"[ConfigAssetCreator] 생성: {path}");
        }
    }
}
