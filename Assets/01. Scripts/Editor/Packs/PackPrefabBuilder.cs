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

        // 실제 카드팩처럼 얇은 호일 봉지: 가운데(몸통)는 3mm, 위아래 압착 띠는 1.2mm로 더 얇음.
        // 접힘 단서는 '표지가 접힌 것'이 아니라 '얇은 윗띠의 한쪽 모서리가 살짝 꺾인 것' (시연 피드백 A3, Quest)
        const float W = 0.075f, H = 0.13f, T = 0.003f;
        const float SealH = 0.014f, SealT = 0.0012f; // 압착 띠 높이 · 두께
        const float ColliderT = 0.006f;              // 잡기 편하도록 충돌 영역은 모양보다 두껍게
        const float CornerLeg = 0.009f;              // 꺾이는 모서리 삼각형의 직각 변 (14mm → 9mm: 더 작게, 시연 피드백)

        [MenuItem("Tools/CardShop/Build Pack Prefab", priority = 21)]
        public static void Build()
        {
            DataPaths.EnsureFolder(MatDir);
            DataPaths.EnsureFolder("Assets/03. Prefabs/Packs");

            Material front = EnsureMaterial("PackFront", new Color(1f, 0.55f, 0.72f));
            Material back = EnsureMaterial("PackBack", new Color(0.36f, 0.27f, 0.55f));
            Material body = EnsureMaterial("PackBody", new Color(0.92f, 0.9f, 0.95f));
            Material seal = EnsureMaterial("PackSeal", new Color(0.78f, 0.74f, 0.9f)); // 호일 압착 띠

            var root = new GameObject("Pack");
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            root.AddComponent<BoxCollider>().size = new Vector3(W, H, ColliderT);

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;

            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            // 몸통(위아래 압착 띠 사이)
            float bodyH = H - 2f * SealH;
            Box(visual, "Body", Vector3.zero, new Vector3(W, bodyH, T), body);
            Quad(visual, "Front", new Vector3(0f, 0f, -T / 2f - 0.0002f), new Vector2(W * 0.94f, bodyH * 0.96f), front, false);
            Quad(visual, "Back", new Vector3(0f, 0f, T / 2f + 0.0002f), new Vector2(W * 0.94f, bodyH * 0.96f), back, true);
            Label(visual);

            // 위아래 압착 띠: 몸통보다 얇고, 가로 줄무늬(톱니)가 있음. 모든 팩 공통 디자인(접힘 단서와는 무관)
            Material crimp = EnsureMaterial("PackCrimp", new Color(0.25f, 0.18f, 0.4f));
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? 1f : -1f;
                string side = s == 0 ? "Top" : "Bottom";
                Box(visual, "Seal" + side, new Vector3(0f, sign * (H / 2f - SealH / 2f), 0f), new Vector3(W, SealH, SealT), seal);
                for (int i = 0; i < 4; i++)
                {
                    float y = sign * (H / 2f - SealH * (0.15f + 0.23f * i));
                    Quad(visual, $"Crimp{side}Front{i}", new Vector3(0f, y, -SealT / 2f - 0.0003f), new Vector2(W * 0.96f, 0.0009f), crimp, false);
                    Quad(visual, $"Crimp{side}Back{i}", new Vector3(0f, y, SealT / 2f + 0.0003f), new Vector2(W * 0.96f, 0.0009f), crimp, true);
                }
            }

            // 접힘 단서: 윗 압착 띠의 오른쪽 모서리가 살짝 꺾임 (같은 호일 색의 얇은 삼각형). 빗변 중앙이 경첩.
            // 접힘 팩에서만 켜집니다(PackView.Init).
            Transform hinge = new GameObject("FoldHinge").transform;
            hinge.SetParent(visual, false);
            hinge.localPosition = new Vector3(W / 2f - CornerLeg / 2f, H / 2f - CornerLeg / 2f, -SealT / 2f - 0.0004f);
            hinge.localRotation = Quaternion.Euler(0f, 0f, -45f); // 로컬 X = 빗변 방향, 로컬 +Y = 모서리 쪽
            var flap = new GameObject("CornerFlap", typeof(MeshFilter), typeof(MeshRenderer));
            flap.transform.SetParent(hinge, false);
            flap.GetComponent<MeshFilter>().sharedMesh = BuildCornerMesh();
            var flapRenderer = flap.GetComponent<MeshRenderer>();
            flapRenderer.sharedMaterials = new[] { seal, seal };
            flapRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var view = root.AddComponent<PackView>();
            var so = new SerializedObject(view);
            so.FindProperty("foldHinge").objectReferenceValue = hinge;
            so.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<TearGestureOpen>(); // 왼손 그립 → 눈앞 고정, 오른손 트리거로 자르기. 대체안: ActivatePackOpen
            // 개봉 후 윗 압착 띠가 뜯겨 날아가는 연출 (시연 피드백 A4). 값은 프리팹에서 조정.
            // 위 "SealTop" · "CrimpTop*" · "FoldHinge" 이름으로 띠 조각을 찾으므로 이름을 바꾸면 PackTearEffect도 함께 바꿉니다.
            root.AddComponent<CardShop.Presentation.PackTearEffect>();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PackPrefabBuilder] {PrefabPath} 생성 완료");
        }

        // 경첩 로컬 좌표의 삼각형 (빗변이 X축 위, 꼭짓점이 +Y). 앞뒤 서브메시로 양면.
        static Mesh BuildCornerMesh()
        {
            const string path = "Assets/03. Prefabs/Packs/PackCornerFlap.asset";
            AssetDatabase.DeleteAsset(path);
            float h = CornerLeg / Mathf.Sqrt(2f);
            var mesh = new Mesh { name = "PackCornerFlap" };
            mesh.vertices = new[]
            {
                new Vector3(-h, 0f, 0f), new Vector3(0f, h, 0f), new Vector3(h, 0f, 0f),
                new Vector3(-h, 0f, 0f), new Vector3(0f, h, 0f), new Vector3(h, 0f, 0f),
            };
            mesh.normals = new[]
            {
                Vector3.back, Vector3.back, Vector3.back,
                Vector3.forward, Vector3.forward, Vector3.forward,
            };
            mesh.uv = new Vector2[6];
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0); // 앞면(-Z에서 보임)
            mesh.SetTriangles(new[] { 3, 5, 4 }, 1); // 뒷면(+Z에서 보임)
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
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
