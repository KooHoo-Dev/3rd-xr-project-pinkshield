using UnityEditor;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 설정 에셋(Assets/02. Data/Configs)을 다시 저장해, 코드에 새로 생긴 칸(기본값)을 에셋 파일에도 적습니다.
    /// 에셋에 없는 칸은 코드 기본값으로 동작하지만 인스펙터 · 파일에서 보이지 않아 값을 조정하기 어렵기 때문 (예: 청소 확률 FIX F1).
    /// 배치 모드: -executeMethod CardShop.EditorTools.ConfigReserializer.RunFromCommandLine -quit
    /// </summary>
    public static class ConfigReserializer
    {
        const string Folder = "Assets/02. Data/Configs";

        [MenuItem("Tools/CardShop/Data/Write Missing Config Fields")]
        public static void Run()
        {
            string[] paths = System.Array.ConvertAll(AssetDatabase.FindAssets("t:ScriptableObject", new[] { Folder }), AssetDatabase.GUIDToAssetPath);
            AssetDatabase.ForceReserializeAssets(paths);
            UnityEngine.Debug.Log($"[ConfigReserializer] {paths.Length}개 다시 저장: {string.Join(", ", paths)}");
        }

        public static void RunFromCommandLine() => Run();
    }
}
