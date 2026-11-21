using System.Collections.Generic;
using CardShop.Decor;
using TMPro;
using CardShop.Cards.EditorTools;
using UnityEditor;
using UnityEngine;

namespace CardShop.EditorTools.Interior
{
    /// <summary>
    /// 에셋에 없는 진열품 · 소품을 만듭니다 (가게_집_인테리어_설계 2-4, D5 · D9).
    /// - 보기 전용 카드팩(DisplayPack): 진짜 Pack 프리팹은 렌더러 30개 + 물리 + 잡기라 장식용으로는 무거워 상자 3개로 단순화
    /// - 슬라임 피규어: 납작한 구 + 눈 + 받침 (색만 바꾼 프리팹 5종)
    /// - 아크릴 스탠드 · 포스터: 기존 카드 아트(04. Art/Cards/Char) 재사용
    /// - 시계(ShopClock) · OPEN 팻말(ShopSign) · 달력(DayCalendar)
    /// 모든 소품의 앞면은 로컬 +Z (LowPolyInterior 가구와 같은 규칙).
    /// </summary>
    public static class DecorFactory
    {
        public const string PrefabDir = "Assets/03. Prefabs/Interior";
        public const string MatDir = "Assets/05. Shaders/Interior/Materials";
        const string PackMatDir = "Assets/05. Shaders/Packs/Materials";
        const string ArtDir = "Assets/04. Art/Cards/Char";
        const string FontPath = DataPaths.Font;

        public static readonly string[] SlimeColors = { "Pink", "Sky", "Lime", "Orange", "Lavender" };
        static readonly Color[] SlimeColorValues =
        {
            new Color(1f, 0.55f, 0.72f), new Color(0.45f, 0.75f, 1f), new Color(0.55f, 0.9f, 0.4f),
            new Color(1f, 0.65f, 0.3f), new Color(0.72f, 0.6f, 1f),
        };
        static readonly string[] PackColors = { "Pink", "Purple", "Gold", "Mint" };
        static readonly Color[] PackColorValues =
        {
            new Color(1f, 0.55f, 0.72f), new Color(0.45f, 0.32f, 0.7f), new Color(0.95f, 0.78f, 0.3f), new Color(0.45f, 0.85f, 0.75f),
        };
        // 스탠드 · 포스터에 쓸 카드 아트 (수퍼레어 이상 위주: 가게에 걸어 둘 만한 카드)
        public static readonly string[] ShowcaseArt = { "F_L", "W_U", "E_SR", "A_SR", "W_L", "F_U", "E_L", "A_R1" };

        // 진열 팩은 수백 개라 렌더러를 줄이려고 몸통 + 위아래 압착 띠를 메시 하나(서브메시 2개)로 합침
        // 11-22 D1: 앞면 MALLANG 그림은 DisplayPackMallangBaker(메뉴)가 이 프리팹의 메시 · 재질을 바꿔 입힘 — 프리팹을 지우고 다시 만들면 그 메뉴를 다시 실행
        public static GameObject DisplayPack(int color) => LoadOrBuild($"DisplayPack_{PackColors[color % PackColors.Length]}", root =>
        {
            Material body = Placer.Mat(MatDir, $"DisplayPack_{PackColors[color % PackColors.Length]}", PackColorValues[color % PackColorValues.Length]);
            Material seal = PackMaterial("PackSeal", new Color(0.78f, 0.74f, 0.9f));
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = PackMesh();
            root.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { body, seal };
        });

        static Mesh PackMesh()
        {
            string path = $"{PrefabDir}/Decor/DisplayPackMesh.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            const float W = 0.075f, H = 0.13f, T = 0.008f, Seal = 0.014f;
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Mesh Part(params Matrix4x4[] boxes)
            {
                var ci = new CombineInstance[boxes.Length];
                for (int i = 0; i < boxes.Length; i++) ci[i] = new CombineInstance { mesh = cube, transform = boxes[i] };
                var m = new Mesh();
                m.CombineMeshes(ci, true, true);
                return m;
            }
            Mesh bodyPart = Part(Matrix4x4.TRS(new Vector3(0f, H / 2f, 0f), Quaternion.identity, new Vector3(W, H - 2f * Seal, T)));
            Mesh sealPart = Part(
                Matrix4x4.TRS(new Vector3(0f, H - Seal / 2f, 0f), Quaternion.identity, new Vector3(W, Seal, T * 0.4f)),
                Matrix4x4.TRS(new Vector3(0f, Seal / 2f, 0f), Quaternion.identity, new Vector3(W, Seal, T * 0.4f)));
            var mesh = new Mesh { name = "DisplayPackMesh" };
            mesh.CombineMeshes(new[] { new CombineInstance { mesh = bodyPart }, new CombineInstance { mesh = sealPart } }, false, false);
            mesh.RecalculateBounds();
            DataPaths.EnsureFolder($"{PrefabDir}/Decor");
            AssetDatabase.CreateAsset(mesh, path);
            Object.DestroyImmediate(bodyPart);
            Object.DestroyImmediate(sealPart);
            return mesh;
        }

        public static GameObject BoosterBox(int color) => LoadOrBuild($"BoosterBox_{PackColors[color % PackColors.Length]}", root =>
        {
            Material body = Placer.Mat(MatDir, $"DisplayPack_{PackColors[color % PackColors.Length]}", PackColorValues[color % PackColorValues.Length]);
            Material band = PackMaterial("PackBack", new Color(0.36f, 0.27f, 0.55f));
            Placer.Prim(root, PrimitiveType.Cube, "Box", new Vector3(0f, 0.06f, 0f), new Vector3(0.2f, 0.12f, 0.13f), body);
            Placer.Prim(root, PrimitiveType.Cube, "Band", new Vector3(0f, 0.06f, 0.0005f), new Vector3(0.202f, 0.035f, 0.132f), band);
        });

        public static GameObject SlimeFigure(int color) => LoadOrBuild($"SlimeFigure_{SlimeColors[color % SlimeColors.Length]}", root =>
        {
            Material body = Placer.Mat(MatDir, $"Slime_{SlimeColors[color % SlimeColors.Length]}", SlimeColorValues[color % SlimeColorValues.Length]);
            body.SetFloat("_Smoothness", 0.7f);
            Material eye = Placer.Mat(MatDir, "Slime_Eye", new Color(0.08f, 0.06f, 0.1f));
            Material shine = Placer.Mat(MatDir, "Slime_Shine", Color.white);
            Material stand = Placer.Mat(MatDir, "Figure_Base", new Color(0.2f, 0.2f, 0.24f));
            Placer.Prim(root, PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.006f, 0f), new Vector3(0.12f, 0.006f, 0.12f), stand);
            Placer.Prim(root, PrimitiveType.Sphere, "Body", new Vector3(0f, 0.012f + 0.036f, 0f), new Vector3(0.1f, 0.075f, 0.1f), body);
            Placer.Prim(root, PrimitiveType.Sphere, "EyeL", new Vector3(-0.02f, 0.06f, 0.043f), Vector3.one * 0.014f, eye);
            Placer.Prim(root, PrimitiveType.Sphere, "EyeR", new Vector3(0.02f, 0.06f, 0.043f), Vector3.one * 0.014f, eye);
            Placer.Prim(root, PrimitiveType.Sphere, "Shine", new Vector3(-0.025f, 0.075f, 0.03f), Vector3.one * 0.012f, shine);
        });

        public static GameObject AcrylicStand(int art) => LoadOrBuild($"AcrylicStand_{ShowcaseArt[art % ShowcaseArt.Length]}", root =>
        {
            string id = ShowcaseArt[art % ShowcaseArt.Length];
            Material face = ArtMaterial(id);
            Material acrylic = Placer.Mat(MatDir, "Acrylic", new Color(0.85f, 0.95f, 1f, 0.35f), transparent: true);
            Placer.Prim(root, PrimitiveType.Cube, "Base", new Vector3(0f, 0.004f, 0f), new Vector3(0.08f, 0.008f, 0.04f), acrylic);
            // Quad는 -Z 쪽에서 보이므로 180도 돌려 +Z(앞)에서 보이게
            Placer.Prim(root, PrimitiveType.Quad, "Art", new Vector3(0f, 0.008f + 0.065f, 0f), new Vector3(0.1f, 0.13f, 1f), face, Quaternion.Euler(0f, 180f, 0f));
        });

        // ------------------------------------------------------------------ 장면에 직접 조립하는 소품

        /// <summary>액자(LowPolyInterior Picture_02) + 배경 + 카드 아트. 앞면이 forward 방향, 뒷면이 벽에 붙음.</summary>
        public static GameObject Poster(Transform parent, Vector3 wallPoint, float yaw, int art, Color background)
        {
            GameObject frame = Placer.Place(parent, ExternalArt.LowPoly("Room/Picture_02"), wallPoint, BackAlign(yaw), yaw, name: "Poster");
            if (frame == null) return null;
            Placer.NoShadows(frame);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 fwd = rot * Vector3.forward;
            Bounds b = Placer.WorldBounds(frame);
            Vector3 face = b.center + fwd * (Depth(b, fwd) + 0.003f);
            Material bg = Placer.Mat(MatDir, $"PosterBg_{ColorUtility.ToHtmlStringRGB(background)}", background);
            var bgGo = Placer.Prim(frame.transform.parent, PrimitiveType.Quad, "PosterBg", face, new Vector3(0.54f, 0.74f, 1f), bg, rot * Quaternion.Euler(0f, 180f, 0f));
            var artGo = Placer.Prim(frame.transform.parent, PrimitiveType.Quad, "PosterArt", face + fwd * 0.002f, new Vector3(0.5f, 0.66f, 1f), ArtMaterial(ShowcaseArt[art % ShowcaseArt.Length]), rot * Quaternion.Euler(0f, 180f, 0f));
            bgGo.transform.SetParent(frame.transform, true);
            artGo.transform.SetParent(frame.transform, true);
            Placer.MakeStatic(frame);
            return frame;
        }

        /// <summary>벽시계 + 바늘 + ShopClock. wallPoint는 시계 뒷면이 닿는 벽 위 점.</summary>
        public static GameObject Clock(Transform parent, Vector3 wallPoint, float yaw)
        {
            GameObject clock = Placer.Place(parent, ExternalArt.LowPoly("Room/Clock_01"), wallPoint, BackAlign(yaw), yaw, name: "WallClock");
            if (clock == null) return null;
            Bounds b = Placer.WorldBounds(clock);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 fwd = rot * Vector3.forward;

            var root = new GameObject("ShopClock");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(b.center + fwd * (Depth(b, fwd) + 0.004f), rot);
            Material black = Placer.Mat(MatDir, "ClockHand", new Color(0.08f, 0.08f, 0.1f));
            Transform hour = Hand(root.transform, "HourHand", 0.09f, 0.012f, black, 0f);
            Transform minute = Hand(root.transform, "MinuteHand", 0.15f, 0.008f, black, 0.002f);
            clock.transform.SetParent(root.transform, true);

            var comp = root.AddComponent<ShopClock>();
            var so = new SerializedObject(comp);
            so.FindProperty("hourHand").objectReferenceValue = hour;
            so.FindProperty("minuteHand").objectReferenceValue = minute;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // 바늘: 피벗(시계 중심)에서 +Y로 뻗은 막대. 회전은 피벗 오브젝트가 함.
        static Transform Hand(Transform parent, string name, float length, float width, Material mat, float zOffset)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(0f, 0f, zOffset);
            Placer.Prim(pivot, PrimitiveType.Cube, "Bar", new Vector3(0f, length * 0.4f, 0f), new Vector3(width, length, 0.003f), mat);
            return pivot;
        }

        /// <summary>OPEN / CLOSED 팻말 (ShopSign). center는 판 중심, 앞면이 forward.</summary>
        public static GameObject Sign(Transform parent, Vector3 center, float yaw)
        {
            var root = new GameObject("ShopSign");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            Material boardMat = Placer.Mat(MatDir, "SignBoard", new Color(0.3f, 0.85f, 0.45f));
            GameObject board = Placer.Prim(root.transform, PrimitiveType.Cube, "Board", Vector3.zero, new Vector3(0.62f, 0.26f, 0.02f), boardMat);
            TMP_Text text = Text(root.transform, "OPEN", new Vector3(0f, 0f, 0.012f), 0.55f, new Vector2(0.6f, 0.24f), Color.white);
            var sign = root.AddComponent<ShopSign>();
            var so = new SerializedObject(sign);
            so.FindProperty("label").objectReferenceValue = text;
            so.FindProperty("board").objectReferenceValue = board.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>가게 정면 큰 간판 (ShopFrontSign, DEC-027). center는 판 중심, 앞면이 forward. 진보라 판 + 분홍 테두리 + 글자</summary>
        public static GameObject FrontSign(Transform parent, Vector3 center, float yaw, Vector2 size)
        {
            var root = new GameObject("ShopFrontSign");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            Placer.Prim(root.transform, PrimitiveType.Cube, "Frame", new Vector3(0f, 0f, -0.01f), new Vector3(size.x + 0.08f, size.y + 0.08f, 0.06f), Placer.Mat(MatDir, "FrontSignFrame", CardShop.UI.UiTheme.Accent));
            Placer.Prim(root.transform, PrimitiveType.Cube, "Board", new Vector3(0f, 0f, 0.012f), new Vector3(size.x, size.y, 0.03f), Placer.Mat(MatDir, "FrontSignBoard", CardShop.UI.UiTheme.Panel));
            TMP_Text text = Text(root.transform, "Slime CardShop", new Vector3(0f, 0f, 0.03f), 2.4f, size * 0.94f, Color.white);
            text.enableAutoSizing = true;
            text.fontSizeMin = 1f;
            text.fontSizeMax = 2.4f;
            var sign = root.AddComponent<ShopFrontSign>();
            var so = new SerializedObject(sign);
            so.FindProperty("label").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>벽 달력 (DayCalendar). center는 판 중심, 앞면이 forward.</summary>
        public static GameObject Calendar(Transform parent, Vector3 center, float yaw)
        {
            var root = new GameObject("DayCalendar");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            Placer.Prim(root.transform, PrimitiveType.Cube, "Paper", Vector3.zero, new Vector3(0.32f, 0.4f, 0.01f), Placer.Mat(MatDir, "CalendarPaper", new Color(0.97f, 0.95f, 0.9f)));
            Placer.Prim(root.transform, PrimitiveType.Cube, "Header", new Vector3(0f, 0.16f, 0.002f), new Vector3(0.32f, 0.08f, 0.01f), Placer.Mat(MatDir, "CalendarHeader", new Color(0.85f, 0.35f, 0.4f)));
            TMP_Text text = Text(root.transform, "DAY\n1", new Vector3(0f, -0.03f, 0.007f), 0.9f, new Vector2(0.3f, 0.3f), new Color(0.2f, 0.18f, 0.22f));
            var cal = root.AddComponent<DayCalendar>();
            var so = new SerializedObject(cal);
            so.FindProperty("label").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // 월드 텍스트. TMP 3D 글자는 로컬 -Z 쪽에서 읽히므로 180도 돌려 +Z(앞)에서 읽히게.
        public static TMP_Text Text(Transform parent, string s, Vector3 localPos, float size, Vector2 box, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) tmp.font = font;
            tmp.text = s;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            ((RectTransform)go.transform).sizeDelta = box;
            return tmp;
        }

        // ------------------------------------------------------------------ 공용

        /// <summary>앞면이 yaw 방향일 때 뒷면(벽 쪽)을 가리키는 align 값. 90도 단위 yaw만.</summary>
        public static Vector3 BackAlign(float yaw)
        {
            Vector3 f = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            return new Vector3(-Mathf.Round(f.x), 0f, -Mathf.Round(f.z));
        }

        /// <summary>경계 상자의 forward 방향 반 두께</summary>
        static float Depth(Bounds b, Vector3 fwd) => Mathf.Abs(fwd.x) * b.extents.x + Mathf.Abs(fwd.z) * b.extents.z;

        static Material PackMaterial(string name, Color fallback)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{PackMatDir}/{name}.mat");
            return mat != null ? mat : Placer.Mat(MatDir, name, fallback);
        }

        static Material ArtMaterial(string id)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/{id}_char.png");
            if (tex == null) Debug.LogWarning($"[DecorFactory] 카드 아트 없음: {ArtDir}/{id}_char.png");
            return Placer.Mat(MatDir, $"Art_{id}", Color.white, tex, "Universal Render Pipeline/Unlit", cutout: true);
        }

        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        /// <summary>프리팹이 있으면 불러오고, 없으면 build로 만들어 저장</summary>
        static GameObject LoadOrBuild(string name, System.Action<Transform> build)
        {
            string path = $"{PrefabDir}/Decor/{name}.prefab";
            if (cache.TryGetValue(path, out GameObject cached) && cached != null) return cached;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return cache[path] = existing;

            DataPaths.EnsureFolder($"{PrefabDir}/Decor");
            var root = new GameObject(name);
            try
            {
                build(root.transform);
                Placer.NoShadows(root); // 작은 소품은 그림자 생략 (Quest 그림자 비용)
                return cache[path] = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>진열 소품 프리팹을 지우고 다시 만들 때 (모양을 바꾼 뒤)</summary>
        public static void ClearCache() => cache.Clear();
    }
}
