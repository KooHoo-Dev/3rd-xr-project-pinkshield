using System.IO;
using UnityEditor;
using UnityEngine;

namespace CardShop.EditorTools.Interior
{
    /// <summary>
    /// 가게 장식장 카드팩(DisplayPack_*, 276개)을 MALLANG 팩 모양으로 (빌드피드백_11-22_FIX설계 D1).
    /// 진짜 팩 프리팹(렌더러 약 30개 + 물리 + 잡기)은 무거워 쓰지 않고, 지금 메시 모양 그대로 앞면에 팩 그림을 입힙니다.
    /// ① 원본 앞면 그림(pack_front_mallang.png)의 가운데 분홍 슬라임만 색을 돌려 4색(분홍 · 보라 · 노랑 · 민트, 남색 바탕 그대로 — 사용자 결정 D1)
    /// ② 4장을 한 장(아틀라스)에 모아 재질 1개 → 276개가 같은 재질이라 그리는 비용은 예전(단색 재질 4개)보다 적음
    /// ③ 색마다 앞면 UV만 다른 메시 4개 → DisplayPack_* 프리팹 4개의 메시 · 재질만 바꿈.
    ///    가게 인테리어 프리팹 안의 장식 팩은 이 프리팹의 인스턴스라 배치는 그대로(손으로 고친 것도 그대로) 모두 바뀝니다.
    /// 예전 단색 재질(DisplayPack_*.mat)은 부스터 박스가 같이 쓸 수 있어 건드리지 않습니다.
    /// </summary>
    public static class DisplayPackMallangBaker
    {
        const string SourcePng = "Assets/04. Art/Packs/pack_front_mallang.png";
        const string AtlasPng = "Assets/04. Art/Packs/pack_front_mallang_display_atlas.png";
        const string MatPath = DecorFactory.MatDir + "/DisplayPackMallang.mat";
        const string MeshDir = DecorFactory.PrefabDir + "/Decor";
        const string PackSealMat = "Assets/05. Shaders/Packs/Materials/PackSeal.mat";

        // DecorFactory.PackColors 순서와 같음 (프리팹 이름 DisplayPack_Pink · Purple · Gold · Mint)
        static readonly string[] Names = { "Pink", "Purple", "Gold", "Mint" };
        static readonly float[] Hues = { -1f, 268f / 360f, 44f / 360f, 160f / 360f }; // -1 = 원본 분홍 그대로
        const int CellW = 256, CellH = 356, AtlasW = 1024, AtlasH = 512;

        // 메시 크기 (DecorFactory.PackMesh와 같음)
        const float W = 0.075f, H = 0.13f, T = 0.008f, Seal = 0.014f;

        [MenuItem("Tools/CardShop/Interior/장식 팩을 MALLANG 팩으로 (D1)", priority = 66)]
        public static void Bake()
        {
            Texture2D atlas = BakeAtlas();
            if (atlas == null) return;
            Material mat = BakeMaterial(atlas);
            var seal = AssetDatabase.LoadAssetAtPath<Material>(PackSealMat);
            int done = 0;
            for (int i = 0; i < Names.Length; i++)
            {
                Mesh mesh = BakeMesh(i);
                string prefab = $"{DecorFactory.PrefabDir}/Decor/DisplayPack_{Names[i]}.prefab";
                if (!File.Exists(prefab)) { Debug.LogWarning($"[DisplayPackMallangBaker] {prefab} 없음 — 건너뜀"); continue; }
                GameObject root = PrefabUtility.LoadPrefabContents(prefab);
                try
                {
                    root.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var r = root.GetComponent<MeshRenderer>();
                    r.sharedMaterials = seal != null ? new[] { mat, seal } : new[] { mat, r.sharedMaterials.Length > 1 ? r.sharedMaterials[1] : mat };
                    PrefabUtility.SaveAsPrefabAsset(root, prefab);
                    done++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[DisplayPackMallangBaker] 장식 팩 프리팹 {done}개를 MALLANG 그림으로 바꿨습니다 (아틀라스 {AtlasPng}).");
        }

        // ------------------------------------------------------------------ 그림

        static Texture2D BakeAtlas()
        {
            if (!File.Exists(SourcePng)) { Debug.LogError($"[DisplayPackMallangBaker] 원본 그림 없음: {SourcePng}"); return null; }
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(SourcePng)); // 가져오기 설정(읽기 가능)과 상관없이 원본 파일로 읽음

            var atlas = new Texture2D(AtlasW, AtlasH, TextureFormat.RGBA32, false);
            var fill = new Color32[AtlasW * AtlasH];
            for (int k = 0; k < fill.Length; k++) fill[k] = new Color32(30, 40, 56, 255);
            atlas.SetPixels32(fill);
            for (int i = 0; i < Names.Length; i++)
                for (int y = 0; y < CellH; y++)
                    for (int x = 0; x < CellW; x++)
                    {
                        float u = (x + 0.5f) / CellW, v = (y + 0.5f) / CellH;
                        Color c = src.GetPixelBilinear(u, v);
                        if (Hues[i] >= 0f && InSlime(u, v) && IsPink(c)) c = Recolor(c, Hues[i]);
                        atlas.SetPixel(i * CellW + x, y, c);
                    }
            atlas.Apply();
            File.WriteAllBytes(AtlasPng, atlas.EncodeToPNG());
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(atlas);

            AssetDatabase.ImportAsset(AtlasPng);
            if (AssetImporter.GetAtPath(AtlasPng) is TextureImporter imp)
            {
                imp.maxTextureSize = 1024;
                imp.mipmapEnabled = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.sRGBTexture = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPng);
        }

        // 슬라임이 있는 가운데 원 (원본 720 × 1000에서 가운데 (360, 555), 반지름 약 215 — 위쪽 색 점 · 아래 글자의 분홍은 그대로)
        static bool InSlime(float u, float v)
        {
            float dx = (u - 0.5f) * 720f, dy = ((1f - v) - 0.555f) * 1000f;
            return dx * dx + dy * dy < 215f * 215f;
        }

        static bool IsPink(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out _);
            return s > 0.12f && (h > 0.82f || h < 0.04f);
        }

        static Color Recolor(Color c, float hue)
        {
            Color.RGBToHSV(c, out _, out float s, out float v);
            Color o = Color.HSVToRGB(hue, Mathf.Min(1f, s * 1.1f), v);
            o.a = c.a;
            return o;
        }

        // ------------------------------------------------------------------ 재질 · 메시

        static Material BakeMaterial(Texture2D atlas)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = "DisplayPackMallang" };
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", atlas);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // 몸통(서브메시 0: 앞면 = 그 색 칸, 나머지 면 = 남색 바탕 한 점) + 위아래 압착 띠(서브메시 1). 앞면은 로컬 +Z
        static Mesh BakeMesh(int color)
        {
            string path = $"{MeshDir}/DisplayPackMallangMesh_{Names[color]}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = $"DisplayPackMallangMesh_{Names[color]}" };
            mesh.Clear();

            float u0 = (float)(color * CellW) / AtlasW, u1 = (float)((color + 1) * CellW) / AtlasW, v1 = (float)CellH / AtlasH;
            Vector2 navy = new Vector2(u0 + 0.12f * (u1 - u0), 0.5f * v1); // 앞면 그림 안쪽 남색 자리

            var verts = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var body = new System.Collections.Generic.List<int>();
            var seal = new System.Collections.Generic.List<int>();

            float bodyLo = Seal, bodyHi = H - Seal;
            // 앞면 그림은 팩 전체 높이(띠 포함)에 맞춰 v를 잘라 씀: 띠에 가려지는 위아래는 버림
            float vLo = Seal / H * v1, vHi = (H - Seal) / H * v1;
            AddBox(verts, normals, uvs, body, new Vector3(0f, (bodyLo + bodyHi) / 2f, 0f), new Vector3(W, bodyHi - bodyLo, T),
                (n, p) => n.z > 0.5f
                    ? new Vector2(Mathf.Lerp(u1, u0, (p.x + W / 2f) / W), Mathf.Lerp(vLo, vHi, (p.y - bodyLo) / (bodyHi - bodyLo))) // +Z에서 보면 오른쪽이 -X
                    : navy);
            foreach (float y in new[] { H - Seal / 2f, Seal / 2f })
                AddBox(verts, normals, uvs, seal, new Vector3(0f, y, 0f), new Vector3(W, Seal, T * 0.4f), (n, p) => Vector2.zero);

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(seal, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void AddBox(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<Vector3> normals,
            System.Collections.Generic.List<Vector2> uvs, System.Collections.Generic.List<int> tris, Vector3 center, Vector3 size,
            System.Func<Vector3, Vector3, Vector2> uvOf)
        {
            Vector3 e = size / 2f;
            Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            foreach (Vector3 n in dirs)
            {
                // 면의 두 축 (바깥에서 봤을 때 시계 방향이 앞면이 되게)
                Vector3 a = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 b = Vector3.Cross(n, a);
                int start = verts.Count;
                Vector3[] corners =
                {
                    Vector3.Scale(n - a - b, e), Vector3.Scale(n + a - b, e), Vector3.Scale(n + a + b, e), Vector3.Scale(n - a + b, e),
                };
                foreach (Vector3 c in corners)
                {
                    verts.Add(center + c);
                    normals.Add(n);
                    uvs.Add(uvOf(n, center + c));
                }
                // Unity: 삼각형 (a, b, c)의 Cross(b - a, c - a)가 가리키는 쪽이 보이는 앞면 → 그 방향이 법선과 같게
                bool facesOut = Vector3.Dot(Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]), n) > 0f;
                if (facesOut) tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                else tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
        }
    }
}
