using CardShop.Cards;
using UnityEditor;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 소리 칸 에셋(Assets/Resources/SoundBank.asset)을 만들거나, 새 SoundId가 생기면 빈 칸을 채웁니다.
    /// 이미 넣은 클립은 지우지 않습니다. 게임은 Resources에서 읽으므로 씬 연결이 필요 없습니다.
    /// </summary>
    public static class SoundBankCreator
    {
        public const string Path = "Assets/Resources/SoundBank.asset";

        [MenuItem("Tools/CardShop/Sound/Create Sound Bank", priority = 70)]
        public static void Create()
        {
            CardShop.Cards.EditorTools.DataPaths.EnsureFolder("Assets/Resources");
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(Path);
            bool created = bank == null;
            if (created)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, Path);
            }
            int added = bank.EnsureAllEntries();
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            Selection.activeObject = bank;
            Debug.Log($"[SoundBankCreator] {Path} {(created ? "생성" : "갱신")}: 빈 칸 {added}개 추가 (전체 {bank.entries.Count}칸)");
        }
    }
}
