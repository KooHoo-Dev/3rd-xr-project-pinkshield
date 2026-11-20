using CardShop.Cards.EditorTools;
using CardShop.Customers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 도둑 복면 프리팹을 만듭니다 (무료 모델 ThiefMask.fbx · 검은 니트 복면, 삼각형 2,080개).
    ///   - 임포트 설정: 메시만(머티리얼 · 애니메이션 · 카메라 없음), 텍스처 512 · Quest(Android) ASTC 6x6
    ///   - 머티리얼: URP Lit + 니트 텍스처
    ///   - 프리팹: 원점 = 머리 가운데, +Z = 얼굴 앞 (ThiefBody.WearMask가 이 약속으로 씌움). 크기 · 높이는 프리팹의 Mesh 자식에서 다듬음
    /// 메뉴: Tools/CardShop/초기화/Thief/복면 프리팹 다시 만들기 (손으로 고친 Mesh 자리가 처음 값으로 돌아감)
    /// </summary>
    public static class ThiefMaskBuilder
    {
        public const string Dir = "Assets/99. External Assets/ThiefMask";
        public const string ModelPath = Dir + "/ThiefMask.fbx";
        public const string TexturePath = Dir + "/ThiefMask_Albedo.png";
        public const string MaterialPath = Dir + "/ThiefMask.mat";
        public const string PrefabPath = CustomerAnimatorBuilder.OutDir + "/ThiefMask.prefab";

        // 모델은 cm 단위 · 얼굴이 FBX +X (Unity로 오면 -X). Toon City People 남녀 손님 머리(머리카락 포함)를 덮도록
        // 배치 렌더로 맞춘 값 (11-20): 폭 약 0.34m · 눈 구멍이 손님 눈 높이 · 얼굴 쪽으로 3cm (손님 머리는 뼈보다 얼굴 쪽으로 나와 있음)
        const float MeshScale = 0.8f; // 11-20 사용자가 프리팹에서 1.05 → 0.8로 줄임 (다시 만들어도 유지)
        static readonly Vector3 MeshOffset = new Vector3(0f, -0.114f, 0.03f); // 머리 가운데(머리 뼈 + maskHeadOffset) 기준

        [MenuItem("Tools/CardShop/초기화/Thief/복면 프리팹 다시 만들기")]
        public static GameObject Build()
        {
            ConfigureModel();
            ConfigureTexture();
            Material mat = EnsureMaterial();

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ModelPath);
            if (mesh == null)
            {
                Debug.LogError($"[ThiefMaskBuilder] 메시 없음: {ModelPath}");
                return null;
            }

            var root = new GameObject("ThiefMask");
            var body = new GameObject("Mesh");
            body.transform.SetParent(root.transform, false);
            body.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // 모델 얼굴(-X) → +Z
            body.transform.localScale = Vector3.one * MeshScale;
            body.transform.localPosition = MeshOffset;
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = body.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off; // 손님 몸도 그림자를 거의 안 씀 · Quest 비용 절약
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;

            DataPaths.EnsureFolder(CustomerAnimatorBuilder.OutDir);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ThiefMaskBuilder] {PrefabPath} 만듦 (bounds {prefab.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size * MeshScale})");
            return prefab;
        }

        /// <summary>프리팹이 없으면 만들고 돌려줌 (ThiefBuilder가 씬을 처음 만들 때).</summary>
        public static GameObject LoadOrBuild()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return prefab != null ? prefab : Build();
        }

        /// <summary>
        /// 열린 Shop 씬의 ThiefDirector가 새 복면 프리팹을 쓰게 하고, 기본 도형으로 만든 예전 복면 원본을 지웁니다.
        /// 배치 모드: -executeMethod CardShop.EditorTools.ThiefMaskBuilder.BuildAndApplyToShopScene
        /// </summary>
        public static void BuildAndApplyToShopScene()
        {
            GameObject prefab = Build();
            if (prefab == null) return;
            var scene = EditorSceneManager.OpenScene(ShopSceneBuilder.ScenePath, OpenSceneMode.Single);
            var director = Object.FindAnyObjectByType<ThiefDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                Debug.LogError("[ThiefMaskBuilder] Shop 씬에 ThiefDirector 없음");
                return;
            }
            var so = new SerializedObject(director);
            SerializedProperty field = so.FindProperty("maskTemplate");
            var old = field.objectReferenceValue as GameObject;
            field.objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (old != null && old.scene.IsValid()) Object.DestroyImmediate(old); // 씬에 있던 기본 도형 복면
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ThiefMaskBuilder] Shop 씬 ThiefDirector.maskTemplate → ThiefMask.prefab");
        }

        static void ConfigureModel()
        {
            if (AssetImporter.GetAtPath(ModelPath) is not ModelImporter mi) return;
            mi.globalScale = 1f;
            mi.useFileScale = true; // cm → m
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.SaveAndReimport();
        }

        static void ConfigureTexture()
        {
            if (AssetImporter.GetAtPath(TexturePath) is not TextureImporter ti) return;
            ti.textureType = TextureImporterType.Default;
            ti.textureShape = TextureImporterShape.Texture2D; // 처음 임포트 때 큐브맵으로 잡혀 머티리얼에 안 들어간 적 있음
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.None; // 이름에 Transparency가 있지만 실제로는 RGB
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 512; // 머리 크기 물건이라 512로 충분 (원본 2048)
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 512,
                format = TextureImporterFormat.ASTC_6x6,
            });
            ti.SaveAndReimport();
        }

        static Material EnsureMaterial()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (tex == null) Debug.LogError($"[ThiefMaskBuilder] 텍스처 없음: {TexturePath}");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null)
            {
                if (mat.GetTexture("_BaseMap") == null && tex != null) // 손으로 고친 값은 두고 빠진 텍스처만 채움
                {
                    mat.SetTexture("_BaseMap", tex);
                    EditorUtility.SetDirty(mat);
                }
                return mat;
            }
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ThiefMask" };
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.1f); // 니트라 반짝이지 않게
            mat.SetFloat("_Cull", (float)CullMode.Off); // 눈 구멍 사이로 안쪽 면이 보여도 비지 않게 (모델이 한 겹)
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }
    }
}
