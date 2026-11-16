using CardShop.Cards.EditorTools;
using CardShop.Interaction;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 카드팩 프리팹(임시 모델)을 만듭니다. 메뉴: Tools > CardShop > Build Pack Prefab
    /// 앞면 = 로컬 -Z (분홍), 뒷면 = +Z (보라). 윗부분 FoldHinge를 돌려 "윗면 접힘" 단서를 표현합니다.
    /// 머티리얼은 이미 있으면 덮어쓰지 않으므로 색을 바꿔도 유지됩니다.
    /// </summary>
    public static class PackPrefabBuilder
    {
        public const string PrefabPath = "Assets/03. Prefabs/Packs/Pack.prefab";
        const string MatDir = "Assets/05. Shaders/Packs/Materials";
        const string FontPath = "Assets/99. External Assets/Font/Noto_Sans_KR/NotoSansKR-Regular SDF.asset";

        const float W = 0.075f, H = 0.13f, T = 0.008f;
        const float FlapH = 0.022f; // 접히는 윗부분 높이

        [MenuItem("Tools/CardShop/Build Pack Prefab", priority = 21)]
        public static void Build()
        {
            DataPaths.EnsureFolder(MatDir);
            DataPaths.EnsureFolder("Assets/03. Prefabs/Packs");

            Material front = EnsureMaterial("PackFront", new Color(1f, 0.55f, 0.72f));
            Material back = EnsureMaterial("PackBack", new Color(0.36f, 0.27f, 0.55f));
            Material body = EnsureMaterial("PackBody", new Color(0.92f, 0.9f, 0.95f));

            var root = new GameObject("Pack");
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            root.AddComponent<BoxCollider>().size = new Vector3(W, H, T * 1.5f);

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;

            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            // 몸통(접히는 윗부분 제외)
            float bodyH = H - FlapH;
            float bodyCenterY = -FlapH / 2f;
            Box(visual, "Body", new Vector3(0f, bodyCenterY, 0f), new Vector3(W, bodyH, T), body);
            Quad(visual, "Front", new Vector3(0f, bodyCenterY, -T / 2f - 0.0002f), new Vector2(W * 0.94f, bodyH * 0.96f), front, false);
            Quad(visual, "Back", new Vector3(0f, bodyCenterY, T / 2f + 0.0002f), new Vector2(W * 0.94f, bodyH * 0.96f), back, true);
            Label(visual);

            // 접히는 윗부분: 경첩(몸통 윗변) 아래 자식으로
            Transform hinge = new GameObject("FoldHinge").transform;
            hinge.SetParent(visual, false);
            hinge.localPosition = new Vector3(0f, H / 2f - FlapH, 0f);
            Box(hinge, "Flap", new Vector3(0f, FlapH / 2f, 0f), new Vector3(W, FlapH, T * 0.8f), body);
            Quad(hinge, "FlapFront", new Vector3(0f, FlapH / 2f, -T * 0.4f - 0.0002f), new Vector2(W * 0.94f, FlapH * 0.8f), front, false);
            Quad(hinge, "FlapBack", new Vector3(0f, FlapH / 2f, T * 0.4f + 0.0002f), new Vector2(W * 0.94f, FlapH * 0.8f), back, true);

            var view = root.AddComponent<PackView>();
            var so = new SerializedObject(view);
            so.FindProperty("foldHinge").objectReferenceValue = hinge;
            so.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<TearGestureOpen>(); // 왼손 그립 → 눈앞 고정, 오른손 트리거로 자르기. 대체안: ActivatePackOpen

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PackPrefabBuilder] {PrefabPath} 생성 완료");
        }

        static void Label(Transform parent)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshPro>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) text.font = font;
            text.text = "SLIME\nPACK";
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 0.22f;
            text.color = Color.white;
            text.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(W * 0.9f, 0.05f);
            rt.localPosition = new Vector3(0f, -0.01f, -T / 2f - 0.0006f);
        }

        static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // Unity Quad는 -Z 쪽에서 보입니다. 뒷면용은 180도 돌립니다.
        static void Quad(Transform parent, string name, Vector3 pos, Vector2 size, Material mat, bool facePlusZ)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = facePlusZ ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material EnsureMaterial(string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.6f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
