using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardShop.EditorTools.Interior
{
    /// <summary>
    /// 외부 에셋(LowPolyInterior · PolygonCity) 경로 찾기와 URP 준비 (가게_집_인테리어_설계 4-1, D6).
    /// - 폴더를 옮겨도 동작하도록 대표 머티리얼 이름으로 루트 폴더를 찾습니다.
    /// - 두 에셋 모두 Built-in Standard 머티리얼이라 URP에서 분홍색 → URP Simple Lit로 바꿉니다 (팔레트 텍스처라 Lit보다 가벼운 Simple Lit로 충분).
    ///   PolygonCity에 들어 있는 URP_ExtractMe.unitypackage를 임포트해도 됩니다. 이미 URP 셰이더인 머티리얼은 건드리지 않습니다.
    /// </summary>
    public static class ExternalArt
    {
        const string SimpleLit = "Universal Render Pipeline/Simple Lit";

        static string lowPolyRoot, cityRoot;

        public static string LowPolyRoot => lowPolyRoot ??= FindRoot("LowPolyInterior_MAIN");
        public static string CityRoot => cityRoot ??= FindRoot("PolygonCity_Mat_01_A");

        /// <summary>LowPolyInterior 프리팹. rel 예: "Walls/WallFloor2_01"</summary>
        public static GameObject LowPoly(string rel) => Load(LowPolyRoot, rel);
        /// <summary>PolygonCity 프리팹. rel 예: "Buildings/SM_Bld_Shop_01"</summary>
        public static GameObject City(string rel) => Load(CityRoot, rel);

        static GameObject Load(string root, string rel)
        {
            if (root == null) return null;
            string path = $"{root}/Prefabs/{rel}.prefab";
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogWarning($"[ExternalArt] 프리팹 없음: {path}");
            return go;
        }

        // 대표 머티리얼(…/Materials/이름.mat)의 두 단계 위 폴더 = 에셋 루트
        static string FindRoot(string materialName)
        {
            foreach (string guid in AssetDatabase.FindAssets(materialName + " t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != materialName) continue;
                string materials = Path.GetDirectoryName(path)?.Replace('\\', '/');
                return Path.GetDirectoryName(materials)?.Replace('\\', '/');
            }
            Debug.LogWarning($"[ExternalArt] '{materialName}' 머티리얼을 찾지 못했습니다. 에셋이 임포트되어 있는지 확인하세요.");
            return null;
        }

        // ------------------------------------------------------------------ URP 변환

        [MenuItem("Tools/CardShop/Interior/Convert External Materials to URP", priority = 60)]
        public static void ConvertMaterialsMenu()
        {
            int n = EnsureUrpMaterials();
            Debug.Log($"[ExternalArt] URP Simple Lit로 바꾼 머티리얼 {n}개");
        }

        /// <summary>두 에셋의 Standard 머티리얼을 URP Simple Lit로 바꿉니다. 바꾼 개수를 돌려줍니다 (이미 바뀐 것은 0).</summary>
        public static int EnsureUrpMaterials()
        {
            int changed = 0;
            foreach (string root in new[] { LowPolyRoot, CityRoot })
            {
                if (root == null) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { root + "/Materials" }))
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (mat == null || mat.shader == null || mat.shader.name != "Standard") continue;
                    bool glass = mat.name.Contains("Glass");
                    ToSimpleLit(mat, glass);
                    changed++;
                }
            }

            // LowPolyInterior 팔레트: 칸(64px) 경계 색이 먼 거리에서 번지지 않게 밉맵 끔
            if (LowPolyRoot != null)
            {
                var importer = AssetImporter.GetAtPath(LowPolyRoot + "/Textures/Texture_01.png") as TextureImporter;
                if (importer != null && importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }
            }
            if (changed > 0) AssetDatabase.SaveAssets();
            return changed;
        }

        static void ToSimpleLit(Material mat, bool transparent)
        {
            Texture main = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            bool emission = mat.IsKeywordEnabled("_EMISSION");
            Texture emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
            Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

            mat.shader = Shader.Find(SimpleLit);
            mat.shaderKeywords = new string[0];
            mat.SetTexture("_BaseMap", main);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.2f);
            mat.SetFloat("_SpecularHighlights", 0f); // 팔레트 로우폴리에 반짝임 불필요 (Quest 비용 절약)
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");

            if (emission && emissionMap != null)
            {
                mat.SetTexture("_EmissionMap", emissionMap);
                mat.SetColor("_EmissionColor", emissionColor);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            SetSurface(mat, transparent);
            EditorUtility.SetDirty(mat);
        }

        /// <summary>URP 머티리얼 불투명/반투명 설정 (인스펙터 GUI가 하는 일을 직접)</summary>
        public static void SetSurface(Material mat, bool transparent)
        {
            mat.SetFloat("_Surface", transparent ? 1f : 0f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            mat.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            mat.SetFloat("_SrcBlendAlpha", transparent ? (float)BlendMode.One : (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            mat.SetFloat("_ZWrite", transparent ? 0f : 1f);
            mat.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            mat.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;
            if (transparent) mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            else mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetShaderPassEnabled("DepthOnly", !transparent);
            mat.SetShaderPassEnabled("ShadowCaster", !transparent);
        }

        // ------------------------------------------------------------------ 렌더 설정

        /// <summary>
        /// 실내 그림자를 또렷하게: URP 그림자 거리 50m → 10m (설계 5-1). 모든 품질 단계의 URP 에셋에 적용.
        /// URP 런타임 어셈블리를 참조하지 않으려고 SerializedObject로 바꿉니다.
        /// </summary>
        public static void ApplyShadowDistance(float distance)
        {
            var seen = new System.Collections.Generic.HashSet<RenderPipelineAsset>();
            for (int i = 0; i < QualitySettings.count; i++)
                SetShadowDistance(QualitySettings.GetRenderPipelineAssetAt(i), distance, seen);
            SetShadowDistance(GraphicsSettings.defaultRenderPipeline, distance, seen);
        }

        static void SetShadowDistance(RenderPipelineAsset asset, float distance, System.Collections.Generic.HashSet<RenderPipelineAsset> seen)
        {
            if (asset == null || !seen.Add(asset)) return;
            var so = new SerializedObject(asset);
            SerializedProperty p = so.FindProperty("m_ShadowDistance");
            if (p == null || Mathf.Approximately(p.floatValue, distance)) return;
            p.floatValue = distance;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }
}
