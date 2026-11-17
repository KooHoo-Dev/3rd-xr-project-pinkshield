using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Cards.EditorTools;
using CardShop.EditorTools.Interior;
using CardShop.Customers;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 바코드 계산에 쓰는 프리팹을 만듭니다 (플레이 피드백: 계산 업무 디테일).
    /// - CustomerItem_Pack / CustomerItem_Card : 팩 · 카드 프리팹에서 보이는 부분만 복사 + 바코드 스티커 + 가격 표시 + ScannableItem
    ///   (원본의 잡기 · 개봉 기능은 떼어 냄. 계산대 위에 놓여 찍히기만 하는 물건)
    /// - BarcodeScanner : 손에 드는 스캐너 (잡기 + 트리거로 스캔)
    /// ShopSceneBuilder → CustomerFloorBuilder가 호출합니다.
    /// </summary>
    public static class CustomerItemBuilder
    {
        public const string PackItemPath = CustomerAnimatorBuilder.OutDir + "/CustomerItem_Pack.prefab";
        public const string CardItemPath = CustomerAnimatorBuilder.OutDir + "/CustomerItem_Card.prefab";
        public const string ScannerPath = CustomerAnimatorBuilder.OutDir + "/BarcodeScanner.prefab";

        const string PackPrefab = "Assets/03. Prefabs/Packs/Pack.prefab";
        const string CardPrefab = "Assets/03. Prefabs/Cards/Card.prefab";
        const string ArtDir = "Assets/04. Art/Customers";
        const string BarcodeTexPath = ArtDir + "/Barcode.png";

        static TMP_FontAsset font;

        public static void Build(TMP_FontAsset labelFont)
        {
            font = labelFont;
            DataPaths.EnsureFolder(CustomerAnimatorBuilder.OutDir);
            DataPaths.EnsureFolder(ArtDir);
            Material barcode = Mat("Barcode", Color.white, BarcodeTexture());

            BuildItem(PackPrefab, PackItemPath, "CustomerItem_Pack", new Vector2(0.035f, 0.018f), barcode, keepCardView: false);
            BuildItem(CardPrefab, CardItemPath, "CustomerItem_Card", new Vector2(0.03f, 0.015f), barcode, keepCardView: true);
            BuildScanner();
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ 계산대 물건

        static void BuildItem(string sourcePath, string outPath, string name, Vector2 stickerSize, Material barcode, bool keepCardView)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) { Debug.LogError($"[CustomerItemBuilder] 원본 프리팹 없음: {sourcePath}"); return; }

            var root = new GameObject(name);
            var visual = (GameObject)Object.Instantiate(source, root.transform);
            visual.name = "Visual";
            // 선반과 같은 방향: 앞면(-Z)이 위, 윗변이 +Z(플레이어 반대쪽)
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            StripToVisual(visual, keepCardView);

            Bounds b = LocalBounds(root);

            var col = root.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = new Vector3(b.size.x, Mathf.Max(b.size.y, 0.01f), b.size.z);

            // 바코드 스티커: 윗면의 플레이어 쪽 오른쪽 모서리
            GameObject sticker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sticker.name = "Barcode";
            Object.DestroyImmediate(sticker.GetComponent<Collider>());
            sticker.transform.SetParent(root.transform, false);
            const float margin = 0.006f;
            sticker.transform.localPosition = new Vector3(b.max.x - stickerSize.x / 2f - margin, b.max.y + 0.0015f, b.min.z + stickerSize.y / 2f + margin);
            sticker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 위에서 보이게
            sticker.transform.localScale = new Vector3(stickerSize.x, stickerSize.y, 1f);
            sticker.GetComponent<MeshRenderer>().sharedMaterial = barcode;

            // 찍은 뒤 뜨는 이름 · 가격 (플레이어 쪽을 봄)
            var labelGo = new GameObject("PriceLabel", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, b.max.y + 0.09f, b.center.z);
            var label = labelGo.AddComponent<TextMeshPro>();
            if (font != null) label.font = font;
            label.fontSize = 0.22f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.95f, 0.4f);
            label.outlineWidth = 0.2f;
            ((RectTransform)labelGo.transform).sizeDelta = new Vector2(0.3f, 0.1f);

            var item = root.AddComponent<ScannableItem>();
            var so = new SerializedObject(item);
            so.FindProperty("priceLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, outPath);
            Object.DestroyImmediate(root);
        }

        // 보이는 부분(메시 · 글자 · CardView)만 남기고 잡기 · 개봉 · 물리 컴포넌트를 뗍니다.
        // 의존 관계(RequireComponent) 때문에 우리 스크립트 → 다른 스크립트 → 물리 순서로 지웁니다.
        static void StripToVisual(GameObject go, bool keepCardView)
        {
            for (int pass = 0; pass < 3; pass++)
                foreach (Component c in go.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || IsVisual(c, keepCardView)) continue;
                    bool ours = c is MonoBehaviour && c.GetType().Namespace != null && c.GetType().Namespace.StartsWith("CardShop");
                    if (pass == 0 && !ours) continue;
                    if (pass == 1 && !(c is MonoBehaviour)) continue;
                    Object.DestroyImmediate(c);
                }
        }

        static bool IsVisual(Component c, bool keepCardView) =>
            c is Transform || c is MeshFilter || c is Renderer || c is CanvasRenderer || c is TMP_Text ||
            (keepCardView && c is CardView);

        static Bounds LocalBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds b = new Bounds(Vector3.zero, Vector3.one * 0.05f);
            bool first = true;
            foreach (Renderer r in renderers)
            {
                if (r.GetComponent<TMP_Text>() != null || r.GetComponent<TMP_SubMesh>() != null) continue; // 글자 영역 제외
                Bounds wb = r.bounds;
                foreach (Vector3 corner in Corners(wb))
                {
                    Vector3 p = root.transform.InverseTransformPoint(corner);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 8; i++)
                yield return new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
        }

        // ------------------------------------------------------------------ 스캐너

        static void BuildScanner()
        {
            var root = new GameObject("BarcodeScanner");
            Material body = Mat("ScannerBody", new Color(0.18f, 0.19f, 0.22f), null);
            Material accent = Mat("ScannerAccent", new Color(1f, 0.75f, 0.2f), null);
            Material glass = Mat("ScannerWindow", new Color(1f, 0.15f, 0.1f), null);
            Material laserMat = Mat("ScannerLaser", new Color(1f, 0.1f, 0.05f), null);

            // 손잡이를 쥐면 머리가 앞(+Z)을 향함. 원점 = 손잡이 가운데(잡는 곳)
            Placer.Prim(root.transform, PrimitiveType.Cube, "Handle", new Vector3(0f, 0f, -0.01f), new Vector3(0.032f, 0.1f, 0.038f), body);
            Placer.Prim(root.transform, PrimitiveType.Cube, "Head", new Vector3(0f, 0.065f, 0.025f), new Vector3(0.05f, 0.045f, 0.12f), body);
            Placer.Prim(root.transform, PrimitiveType.Cube, "Trigger", new Vector3(0f, 0.03f, 0.018f), new Vector3(0.012f, 0.02f, 0.012f), accent);
            Placer.Prim(root.transform, PrimitiveType.Cube, "Window", new Vector3(0f, 0.065f, 0.0855f), new Vector3(0.042f, 0.032f, 0.002f), glass);

            var tip = new GameObject("Tip").transform;
            tip.SetParent(root.transform, false);
            tip.localPosition = new Vector3(0f, 0.065f, 0.09f);

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.03f, 0.02f);
            col.size = new Vector3(0.05f, 0.13f, 0.13f);

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true; // 놓으면 그 자리에 떠 있다가 제자리로 돌아감 (BarcodeScanner)
            rb.useGravity = false;

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;

            var laser = root.AddComponent<LineRenderer>();
            laser.useWorldSpace = true;
            laser.positionCount = 2;
            laser.startWidth = laser.endWidth = 0.002f;
            laser.sharedMaterial = laserMat;
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var scanner = root.AddComponent<BarcodeScanner>();
            var so = new SerializedObject(scanner);
            so.FindProperty("tip").objectReferenceValue = tip;
            so.FindProperty("laser").objectReferenceValue = laser;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ScannerPath);
            Object.DestroyImmediate(root);
        }

        // ------------------------------------------------------------------ 에셋

        static Material Mat(string name, Color color, Texture2D tex)
        {
            string path = $"{ArtDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // 흰 여백 + 굵기가 다른 검은 막대. 실제 규격 바코드가 아니라 "바코드처럼 보이는" 그림
        static Texture2D BarcodeTexture()
        {
            // 같은 시드로 늘 같은 그림이라, 이미 있으면 다시 쓰지 않음 (빌드할 때마다 git 변경이 생기지 않게)
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(BarcodeTexPath);
            if (existing != null) return existing;

            const int w = 128, h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = new Color32[w * h];
            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(15, 15, 15, 255);
            var rng = new System.Random(20261117);
            var column = new bool[w];
            for (int x = 10; x < w - 10;)
            {
                int bar = rng.Next(1, 4), gap = rng.Next(1, 3);
                for (int i = 0; i < bar && x < w - 10; i++) column[x++] = true;
                x += gap;
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = column[x] && y >= 6 && y < h - 6 ? black : white;
            tex.SetPixels32(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(BarcodeTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(BarcodeTexPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(BarcodeTexPath);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(BarcodeTexPath);
        }
    }
}
