using CardShop.EditorTools.Interior;
using UnityEditor;
using UnityEngine;

namespace CardShop.Cards.EditorTools
{
    /// <summary>
    /// 방 업그레이드 목록 에셋(Assets/Resources/UpgradeCatalog.asset)을 만들고, 가구 모델 · 카드 · 택배 상자 참조를 경로로 채웁니다
    /// (집_업그레이드_설계 3장). 가격 · 효과 값은 건드리지 않으므로 인스펙터에서 바꾼 값은 그대로 남습니다.
    /// 메뉴: Tools > CardShop > Data > Build Upgrade Catalog
    /// 배치 모드: -executeMethod CardShop.Cards.EditorTools.UpgradeCatalogBuilder.RunFromCommandLine -quit
    /// </summary>
    public static class UpgradeCatalogBuilder
    {
        public const string AssetPath = "Assets/Resources/" + UpgradeCatalog.ResourcePath + ".asset";
        const string CardPrefabPath = "Assets/03. Prefabs/Cards/Card.prefab";
        const string LowPolyPrefix = "LowPoly:";

        [MenuItem("Tools/CardShop/Data/Build Upgrade Catalog")]
        public static void Run()
        {
            DataPaths.EnsureFolder("Assets/Resources");
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(AssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<UpgradeCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetPath);
                Debug.Log($"[UpgradeCatalogBuilder] 생성: {AssetPath}");
            }

            int filled = 0, missing = 0;
            foreach (UpgradeCatalog.Track t in catalog.tracks)
                foreach (UpgradeCatalog.Level l in t.levels)
                    foreach (UpgradeCatalog.Visual v in l.visuals)
                    {
                        if (v == null || string.IsNullOrEmpty(v.modelPath)) continue;
                        v.model = Resolve(v.modelPath);
                        if (v.model != null) filled++; else missing++;
                    }

            catalog.showcaseCardPrefab = AssetDatabase.LoadAssetAtPath<CardView>(CardPrefabPath);
            if (catalog.showcaseCardPrefab == null) { missing++; Debug.LogWarning($"[UpgradeCatalogBuilder] 카드 프리팹 없음: {CardPrefabPath}"); }
            catalog.deliveryBoxPrefab = string.IsNullOrEmpty(catalog.deliveryBoxPath) ? null : Resolve(catalog.deliveryBoxPath);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UpgradeCatalogBuilder] 모델 {filled}개 연결, 못 찾음 {missing}개 → {AssetPath}");
        }

        public static void RunFromCommandLine() => Run();

        /// <summary>
        /// 분야 · 단계 · 가격 · 모델 목록을 코드 기본값(UpgradeCatalog.CodeDefaults)으로 되돌리고 모델을 다시 연결합니다.
        /// 코드에서 분야를 더하거나(에어컨) 모델을 바꾼 뒤(침대 · 컴퓨터, 11-20 F5 · F6) 씁니다. 인스펙터에서 바꾼 가격도 코드 값으로 돌아갑니다.
        /// 배치 모드: -executeMethod CardShop.Cards.EditorTools.UpgradeCatalogBuilder.ResetTracksToCode -quit
        /// </summary>
        [MenuItem("Tools/CardShop/Data/Reset Upgrade Tracks To Code")]
        public static void ResetTracksToCode()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(AssetPath);
            if (catalog != null)
            {
                catalog.tracks = UpgradeCatalog.CodeDefaults();
                EditorUtility.SetDirty(catalog);
            }
            Run();
            CardShop.EditorTools.UpgradeAnchorEntries.Invalidate();
        }

        static GameObject Resolve(string path)
        {
            GameObject go = path.StartsWith(LowPolyPrefix)
                ? ExternalArt.LowPoly(path.Substring(LowPolyPrefix.Length))
                : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogWarning($"[UpgradeCatalogBuilder] 모델 없음: {path}");
            return go;
        }
    }
}
