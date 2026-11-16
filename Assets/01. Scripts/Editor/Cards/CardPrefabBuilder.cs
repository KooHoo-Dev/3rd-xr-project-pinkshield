using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Cards.EditorTools
{
    /// <summary>
    /// 카드 프리팹과 관련 에셋(메시, 머티리얼, 등급 스타일, 샘플 데이터)을 만듭니다.
    /// 메뉴: Tools > CardShop > Build Card Prefab
    /// 이미 있는 스타일·데이터·머티리얼은 덮어쓰지 않으므로 조정한 값이 유지됩니다.
    /// </summary>
    public static class CardPrefabBuilder
    {
        // 실제 TCG 카드 크기 (63 x 88 mm)
        public const float Width = 0.063f;
        public const float Height = 0.088f;
        const float Thickness = 0.0012f;
        const float CornerRadius = 0.0035f;
        const int CornerSegments = 6;

        const string ArtDir = "Assets/04. Art/Cards";
        const string MeshPath = ArtDir + "/Meshes/CardMesh.asset";
        const string MatDir = "Assets/05. Shaders/Cards/Materials";
        const string BackTexPath = ArtDir + "/Back/card_back_placeholder.png";
        const string PrefabPath = "Assets/03. Prefabs/Cards/Card.prefab";
        const string DataDir = "Assets/02. Data/Cards";
        const string StylesPath = DataDir + "/CardRarityStyles.asset";
        const string SampleDataPath = DataDir + "/F_N1.asset";
        const string SampleCharPath = ArtDir + "/Char/F_N1_char.png";
        const string FontPath = "Assets/99. External Assets/Font/Noto_Sans_KR/NotoSansKR-Regular SDF.asset";

        [MenuItem("Tools/CardShop/Build Card Prefab")]
        public static void Build()
        {
            EnsureFolder(ArtDir + "/Meshes");
            EnsureFolder(MatDir);
            EnsureFolder(ArtDir + "/Back");
            EnsureFolder("Assets/03. Prefabs/Cards");
            EnsureFolder(DataDir);

            Mesh mesh = SaveMesh(CreateCardMesh());
            Texture2D backTex = EnsureBackTexture();
            Material front = EnsureMaterial("CardFront", "CardShop/CardFront", null, Color.white);
            Material back = EnsureMaterial("CardBack", "CardShop/CardSimple", backTex, Color.white);
            Material edge = EnsureMaterial("CardEdge", "CardShop/CardSimple", null, new Color(0.96f, 0.95f, 0.92f));
            CardRarityStyles styles = EnsureStyles();
            CardData sample = EnsureSampleData();

            GameObject root = CreateCardObject(mesh, new[] { front, back, edge }, styles, sample);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            Debug.Log($"[CardPrefabBuilder] Built {PrefabPath}");
        }

        // ---------------------------------------------------------------- Mesh

        /// <summary>둥근 모서리 판 메시. 서브메시 0 앞면(-Z), 1 뒷면(+Z), 2 옆면.</summary>
        static Mesh CreateCardMesh()
        {
            var outline = new List<Vector2>();
            float hw = Width / 2f, hh = Height / 2f, r = CornerRadius;
            Vector2[] centers =
            {
                new Vector2(hw - r, -hh + r), new Vector2(hw - r, hh - r),
                new Vector2(-hw + r, hh - r), new Vector2(-hw + r, -hh + r),
            };
            for (int c = 0; c < 4; c++)
            {
                float start = -90f + 90f * c;
                for (int s = 0; s <= CornerSegments; s++)
                {
                    float a = (start + 90f * s / CornerSegments) * Mathf.Deg2Rad;
                    outline.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }

            int n = outline.Count;
            float zf = -Thickness / 2f, zb = Thickness / 2f;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var front = new List<int>();
            var back = new List<int>();
            var side = new List<int>();

            Vector2 Uv(Vector2 p) => new Vector2(p.x / Width + 0.5f, p.y / Height + 0.5f);

            // 앞면 (부채꼴, -Z를 바라봄)
            int fc = verts.Count;
            verts.Add(new Vector3(0, 0, zf)); normals.Add(Vector3.back); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < n; i++)
            {
                verts.Add(new Vector3(outline[i].x, outline[i].y, zf)); normals.Add(Vector3.back); uvs.Add(Uv(outline[i]));
            }
            for (int i = 0; i < n; i++)
            {
                front.Add(fc); front.Add(fc + 1 + (i + 1) % n); front.Add(fc + 1 + i);
            }

            // 뒷면 (+Z를 바라봄, 뒤에서 봤을 때 바로 읽히도록 u 반전)
            int bc = verts.Count;
            verts.Add(new Vector3(0, 0, zb)); normals.Add(Vector3.forward); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < n; i++)
            {
                Vector2 uv = Uv(outline[i]);
                verts.Add(new Vector3(outline[i].x, outline[i].y, zb)); normals.Add(Vector3.forward); uvs.Add(new Vector2(1f - uv.x, uv.y));
            }
            for (int i = 0; i < n; i++)
            {
                back.Add(bc); back.Add(bc + 1 + i); back.Add(bc + 1 + (i + 1) % n);
            }

            // 옆면
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = outline[i], p1 = outline[(i + 1) % n];
                Vector2 d = p1 - p0;
                Vector3 nrm = new Vector3(d.y, -d.x, 0f).normalized;
                int s = verts.Count;
                verts.Add(new Vector3(p0.x, p0.y, zf));
                verts.Add(new Vector3(p1.x, p1.y, zf));
                verts.Add(new Vector3(p1.x, p1.y, zb));
                verts.Add(new Vector3(p0.x, p0.y, zb));
                for (int k = 0; k < 4; k++) { normals.Add(nrm); uvs.Add(new Vector2(0.5f, 0.5f)); }
                side.Add(s); side.Add(s + 1); side.Add(s + 2);
                side.Add(s); side.Add(s + 2); side.Add(s + 3);
            }

            var mesh = new Mesh { name = "CardMesh", subMeshCount = 3 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(front, 0);
            mesh.SetTriangles(back, 1);
            mesh.SetTriangles(side, 2);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh SaveMesh(Mesh mesh)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, MeshPath);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing); // GUID 유지
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ---------------------------------------------------------------- Assets

        static Material EnsureMaterial(string name, string shaderName, Texture tex, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(Shader.Find(shaderName)) { name = name };
            if (mat.HasProperty("_BaseMap") && tex != null) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>카드 뒷면 임시 텍스처 (보라·청록 그라데이션 + 금색 원 + 슬라임 얼굴). 실제 아트가 오면 교체.</summary>
        static Texture2D EnsureBackTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(BackTexPath);
            if (existing != null) return existing;

            const int w = 512, h = 716;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var violet = new Color(0.27f, 0.16f, 0.45f);
            var teal = new Color(0.1f, 0.45f, 0.5f);
            var gold = new Color(0.95f, 0.78f, 0.35f);
            var slime = new Color(0.55f, 0.85f, 0.75f);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    Vector2 p = new Vector2((u - 0.5f) * w, (v - 0.5f) * h);
                    float dist = p.magnitude;
                    float ang = Mathf.Atan2(p.y, p.x);
                    Color c = Color.Lerp(teal, violet, v);
                    float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 8f)), 6f) * Mathf.Clamp01(1f - dist / 420f);
                    c = Color.Lerp(c, c * 1.35f, rays);
                    if (Mathf.Abs(dist - 150f) < 7f) c = gold;
                    if (dist < 120f)
                    {
                        // 슬라임 얼굴: 아래가 넓은 젤리 + 두 눈 + 하이라이트
                        Vector2 q = new Vector2(p.x, p.y + 15f);
                        float body = (q.x * q.x) / (95f * 95f) + (q.y * q.y) / (80f * 80f) * (q.y > 0 ? 1.3f : 1f);
                        c = body < 1f ? slime : Color.Lerp(c, violet * 0.7f, 0.6f);
                        if (body < 1f)
                        {
                            if ((p - new Vector2(-32f, 5f)).magnitude < 12f || (p - new Vector2(32f, 5f)).magnitude < 12f)
                                c = new Color(0.12f, 0.1f, 0.18f);
                            if ((p - new Vector2(-28f, 10f)).magnitude < 4f || (p - new Vector2(36f, 10f)).magnitude < 4f)
                                c = Color.white;
                        }
                    }
                    if (u < 0.035f || u > 0.965f || v < 0.025f || v > 0.975f) c = gold * 0.9f;
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(BackTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(BackTexPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(BackTexPath);
        }

        static CardRarityStyles EnsureStyles()
        {
            var styles = AssetDatabase.LoadAssetAtPath<CardRarityStyles>(StylesPath);
            if (styles != null) return styles;

            styles = ScriptableObject.CreateInstance<CardRarityStyles>();
            styles.styles = new[]
            {
                new CardRarityStyles.Style
                {
                    label = "노말", frameColor = new Color(0.80f, 0.80f, 0.82f), accentColor = new Color(0.62f, 0.62f, 0.68f),
                },
                new CardRarityStyles.Style
                {
                    label = "레어", frameColor = new Color(0.62f, 0.78f, 0.95f), accentColor = new Color(0.78f, 0.9f, 1f),
                    frameSparkle = 0.6f,
                },
                new CardRarityStyles.Style
                {
                    label = "수퍼레어", frameColor = new Color(0.72f, 0.6f, 0.95f), accentColor = new Color(0.92f, 0.78f, 1f),
                    frameSparkle = 0.8f, artHolo = 0.8f,
                },
                new CardRarityStyles.Style
                {
                    label = "유니크", frameColor = new Color(0.93f, 0.74f, 0.35f), accentColor = new Color(1f, 0.86f, 0.45f),
                    fullArt = true, charScale = 1.15f, charOffsetY = 0.12f,
                    frameSparkle = 1f, artHolo = 0.9f, scrollPattern = 0.7f, rim = 0.6f,
                },
                new CardRarityStyles.Style
                {
                    label = "레전더리", frameColor = new Color(0.95f, 0.5f, 0.32f), accentColor = new Color(1f, 0.78f, 0.38f),
                    fullArt = true, popOut = true, charScale = 1.35f, charOffsetY = 0.14f,
                    frameSparkle = 1f, artHolo = 1f, scrollPattern = 1f, rim = 1f, glow = 1f,
                },
            };
            styles.elementColors = new[]
            {
                new Color(0.7f, 0.7f, 0.72f),  // None
                new Color(1f, 0.55f, 0.25f),   // Fire
                new Color(0.3f, 0.75f, 0.9f),  // Water
                new Color(0.65f, 0.6f, 0.3f),  // Earth
                new Color(0.7f, 0.8f, 1f),     // Air
                new Color(1f, 0.75f, 0.8f),    // Neutral
            };
            AssetDatabase.CreateAsset(styles, StylesPath);
            return styles;
        }

        static CardData EnsureSampleData()
        {
            var data = AssetDatabase.LoadAssetAtPath<CardData>(SampleDataPath);
            if (data != null) return data;

            data = ScriptableObject.CreateInstance<CardData>();
            data.id = "F-N1";
            data.kind = CardKind.Slime;
            data.element = CardElement.Fire;
            data.rarity = CardRarity.Normal;
            data.displayName = "불씨 슬라임";
            data.description = "머리 위의 작은 불꽃은 기분이 좋을 때 더 밝게 타오른다.";
            data.basePrice = 1;
            data.character = AssetDatabase.LoadAssetAtPath<Texture2D>(SampleCharPath);
            AssetDatabase.CreateAsset(data, SampleDataPath);
            return data;
        }

        // ---------------------------------------------------------------- Prefab

        static GameObject CreateCardObject(Mesh mesh, Material[] materials, CardRarityStyles styles, CardData sample)
        {
            var root = new GameObject("Card");

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.01f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(Width, Height, 0.004f);

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;

            // 메시와 글자는 "Visual" 아래에 둡니다. 공개 연출이 Visual만 뒤집어 앞/뒤를 바꿉니다 (CardRevealView).
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            var meshGo = new GameObject("Mesh");
            meshGo.transform.SetParent(visual, false);
            meshGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = meshGo.AddComponent<MeshRenderer>();
            mr.sharedMaterials = materials;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) Debug.LogWarning($"[CardPrefabBuilder] Font not found: {FontPath}");

            // 위치는 카드 UV(0~1) 기준 사각형
            TMP_Text nameText = CreateText(visual, "Text_Name", font, new Rect(0.075f, 0.885f, 0.62f, 0.07f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            TMP_Text rarityText = CreateText(visual, "Text_Rarity", font, new Rect(0.70f, 0.89f, 0.225f, 0.055f), TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            TMP_Text descText = CreateText(visual, "Text_Description", font, new Rect(0.09f, 0.14f, 0.82f, 0.22f), TextAlignmentOptions.TopLeft, FontStyles.Normal);
            TMP_Text priceText = CreateText(visual, "Text_Price", font, new Rect(0.09f, 0.055f, 0.82f, 0.06f), TextAlignmentOptions.MidlineRight, FontStyles.Normal);
            descText.fontSizeMax = 0.32f;
            rarityText.fontSizeMax = 0.4f;
            priceText.fontSizeMax = 0.3f;

            var view = root.AddComponent<CardView>();
            view.Configure(styles, mr, 0, nameText, rarityText, descText, priceText);
            view.Bind(sample);
            return root;
        }

        static TMP_Text CreateText(Transform parent, string name, TMP_FontAsset font, Rect uvRect, TextAlignmentOptions align, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshPro>();
            if (font != null) text.font = font;
            text.fontStyle = style;
            text.alignment = align;
            text.enableAutoSizing = true;
            // TextMeshPro(3D) 글자 크기 1 ≈ 1cm. 자동 크기로 사각형 안에 맞춥니다.
            text.fontSizeMin = 0.05f;
            text.fontSizeMax = 0.5f;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.margin = Vector4.zero;
            text.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(uvRect.width * Width, uvRect.height * Height);
            rt.localPosition = new Vector3(
                (uvRect.center.x - 0.5f) * Width,
                (uvRect.center.y - 0.5f) * Height,
                -Thickness / 2f - 0.0003f);
            return text;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------------------------------------------------------------- Preview (verification)

        /// <summary>
        /// 배치 모드용: 프리팹을 만든 뒤 등급별 미리보기 이미지를 렌더링합니다.
        /// Unity.exe -batchmode -projectPath ... -executeMethod CardShop.Cards.EditorTools.CardPrefabBuilder.BuildFromCommandLine -cardPreviewOut <dir> -quit
        /// </summary>
        public static void BuildFromCommandLine()
        {
            Build();
            string outDir = GetArg("-cardPreviewOut");
            if (!string.IsNullOrEmpty(outDir)) RenderPreviews(outDir);
        }

        [MenuItem("Tools/CardShop/Render Card Previews")]
        static void RenderPreviewsMenu()
        {
            RenderPreviews(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/CardPreviews"));
        }

        static void RenderPreviews(string outDir)
        {
            Directory.CreateDirectory(outDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

            var lightGo = new GameObject("Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(30f, 20f, 0f);

            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.13f, 0.2f);
            cam.fieldOfView = 20f;
            cam.nearClipPlane = 0.01f;
            camGo.transform.position = new Vector3(0, 0, -0.42f);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var sample = AssetDatabase.LoadAssetAtPath<CardData>(SampleDataPath);
            var cards = new List<GameObject>();
            for (int r = 0; r < 5; r++)
            {
                var data = Object.Instantiate(sample);
                data.rarity = (CardRarity)r;
                var card = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                card.GetComponent<Rigidbody>().isKinematic = true;
                card.transform.position = new Vector3((r - 2) * 0.075f, 0, 0);
                card.GetComponent<CardView>().Bind(data);
                foreach (var t in card.GetComponentsInChildren<TMP_Text>()) t.ForceMeshUpdate();
                cards.Add(card);
            }

            Capture(cam, Path.Combine(outDir, "card_front.png"));

            for (int r = 0; r < 5; r++) cards[r].transform.rotation = Quaternion.Euler(-12f, 28f, 0f);
            Capture(cam, Path.Combine(outDir, "card_angled.png"));

            for (int r = 0; r < 5; r++) cards[r].transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Capture(cam, Path.Combine(outDir, "card_back.png"));

            cam.fieldOfView = 14f;
            for (int r = 0; r < 5; r++) cards[r].transform.rotation = Quaternion.identity;
            camGo.transform.position = new Vector3(0, 0, -0.42f);
            for (int r = 0; r < 5; r++) cards[r].SetActive(r == 0);
            cards[0].transform.position = Vector3.zero;
            Capture(cam, Path.Combine(outDir, "card_closeup_normal.png"), 900, 1200);
            cards[0].SetActive(false);
            cards[4].SetActive(true);
            cards[4].transform.position = Vector3.zero;
            Capture(cam, Path.Combine(outDir, "card_closeup_legendary.png"), 900, 1200);

            Debug.Log($"[CardPrefabBuilder] Previews written to {outDir}");
        }

        static void Capture(Camera cam, string path, int w = 2400, int h = 800)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        static string GetArg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
