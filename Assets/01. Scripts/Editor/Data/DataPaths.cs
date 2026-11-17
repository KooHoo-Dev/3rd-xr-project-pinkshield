using UnityEditor;

namespace CardShop.Cards.EditorTools
{
    /// <summary>에디터 도구가 쓰는 에셋 경로 모음. 폴더를 옮기면 여기만 고치세요.</summary>
    public static class DataPaths
    {
        public const string Root = "Assets/02. Data";
        public const string CardsDir = Root + "/Cards";
        public const string ConfigsDir = Root + "/Configs";

        public const string CardDatabase = Root + "/CardDatabase.asset";
        public const string RarityTable = ConfigsDir + "/RarityTable.asset";
        public const string PackOdds = ConfigsDir + "/PackOddsConfig.asset";
        public const string Economy = ConfigsDir + "/EconomyConfig.asset";
        public const string Campaign = ConfigsDir + "/CampaignConfig.asset";
        public const string Score = ConfigsDir + "/ScoreConfig.asset";
        public const string RevealProfile = ConfigsDir + "/RarityRevealProfile.asset";
        public const string MorningLines = ConfigsDir + "/MorningLineSet.asset";

        public const string ArtRoot = "Assets/04. Art/Cards";
        public const string ArtChar = ArtRoot + "/Char";
        public const string ArtBg = ArtRoot + "/BG";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
