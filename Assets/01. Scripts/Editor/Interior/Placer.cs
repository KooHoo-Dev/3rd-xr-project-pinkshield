using System.Collections.Generic;
using CardShop.Cards.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardShop.EditorTools.Interior
{
    /// <summary>
    /// 외부 에셋 프리팹을 "실측 크기(렌더러 경계 상자)" 기준으로 놓는 도구.
    /// 모델마다 피벗 위치가 달라도(바닥은 아래면, 문은 경첩, 조명은 윗면) 경계 상자의 어느 점을 목표 위치에 맞출지(align)로 놓습니다.
    /// align 각 축은 -1(최소) ~ 0(가운데) ~ 1(최대). 예: 바닥에 놓기 (0,-1,0), 뒷벽에 등을 붙이기 (0,-1,-1).
    /// 루트는 원점 · 회전 없음이라고 가정합니다(프리팹 빌드용 미리보기 씬). 회전은 90도 단위만 써야 경계 상자가 정확합니다.
    /// </summary>
    public static class Placer
    {
        public enum Col
        {
            /// <summary>콜라이더 전부 제거 (장식)</summary>
            None,
            /// <summary>에셋의 MeshCollider 유지 (벽: 창 · 출입구 구멍까지 정확)</summary>
            Mesh,
            /// <summary>제거 후 경계 상자 BoxCollider 하나 (물건을 올려놓는 가구)</summary>
            Box,
        }

        public static readonly Vector3 Bottom = new Vector3(0f, -1f, 0f);

        public static GameObject Place(Transform parent, GameObject prefab, Vector3 target, Vector3 align,
            float yaw = 0f, float scale = 1f, Col col = Col.None, string name = null, Vector3? euler = null, Vector3? scale3 = null,
            bool unpack = true)
        {
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            // 외부 에셋은 풀어서(콜라이더 정리 자유롭게), 우리 소품 프리팹(진열 팩 · 피규어)은 연결 유지(프리팹 하나 고치면 전부 바뀜)
            if (unpack) PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.SetParent(parent, false);
            if (name != null) go.name = name;
            go.transform.localRotation = Quaternion.Euler(euler ?? new Vector3(0f, yaw, 0f));
            go.transform.localScale = Vector3.Scale(go.transform.localScale, scale3 ?? Vector3.one * scale);
            go.transform.localPosition = Vector3.zero;

            Bounds b = WorldBounds(go);
            Vector3 anchor = b.center + Vector3.Scale(b.extents, align);
            go.transform.position += target - anchor;

            ApplyColliders(go, col);
            MakeStatic(go);
            return go;
        }

        /// <summary>경계 상자가 정확히 size가 되도록 축별 배율을 맞춰 놓음 (기능 위치가 정해진 테이블 등)</summary>
        public static GameObject PlaceFit(Transform parent, GameObject prefab, Vector3 target, Vector3 align, Vector3 size,
            float yaw = 0f, Col col = Col.None, string name = null)
        {
            if (prefab == null) return null;
            GameObject go = Place(parent, prefab, target, align, yaw, 1f, Col.None, name);
            Bounds b = WorldBounds(go);
            // 로컬 축마다 월드에서 어느 축을 향하는지 보고(90도 회전) 그 월드 축의 비율을 곱함
            Vector3 ls = go.transform.localScale;
            for (int i = 0; i < 3; i++)
            {
                Vector3 axis = go.transform.TransformDirection(i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward);
                int w = Mathf.Abs(axis.x) > 0.5f ? 0 : Mathf.Abs(axis.y) > 0.5f ? 1 : 2;
                if (b.size[w] > 1e-4f) ls[i] *= size[w] / b.size[w];
            }
            go.transform.localScale = ls;

            b = WorldBounds(go);
            go.transform.position += target - (b.center + Vector3.Scale(b.extents, align));
            ApplyColliders(go, col);
            return go;
        }

        public static Bounds WorldBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        public static void ApplyColliders(GameObject go, Col col)
        {
            if (col == Col.Mesh) return;
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            if (col != Col.Box) return;
            Bounds b = WorldBounds(go);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            Vector3 s = go.transform.InverseTransformVector(b.size);
            box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }

        /// <summary>정적 배칭 · 오클루전 대상 (움직이지 않는 인테리어)</summary>
        public static void MakeStatic(GameObject go)
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }

        /// <summary>면 하나짜리 판(뒤집은 천장 등)이 해 쪽에서 봐도 그림자를 드리우게</summary>
        public static void TwoSidedShadows(GameObject go)
        {
            if (go == null) return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.TwoSided;
        }

        public static void NoShadows(GameObject go)
        {
            if (go == null) return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// 가구의 위를 향한 넓은 면(선반 판) 높이를 메시에서 직접 찾습니다. 선반에 물건을 정확히 올리기 위함.
        /// minArea보다 넓은 위쪽 면만, 1cm 단위로 묶고 아래에서 위로 정렬.
        /// </summary>
        public static List<float> ShelfLevels(GameObject go, float minArea = 0.04f)
        {
            var areaByLevel = new Dictionary<int, float>();
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue; // 에디터에서는 Read/Write가 꺼진 메시도 읽을 수 있음
                Vector3[] v = mesh.vertices;
                int[] tris = mesh.triangles;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(v[tris[i]]);
                    Vector3 b = m.MultiplyPoint3x4(v[tris[i + 1]]);
                    Vector3 c = m.MultiplyPoint3x4(v[tris[i + 2]]);
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    float area = n.magnitude * 0.5f;
                    if (area < 1e-6f || n.normalized.y < 0.95f) continue;
                    int key = Mathf.RoundToInt((a.y + b.y + c.y) / 3f * 100f);
                    areaByLevel.TryGetValue(key, out float sum);
                    areaByLevel[key] = sum + area;
                }
            }
            var levels = new List<float>();
            foreach (var kv in areaByLevel) if (kv.Value >= minArea) levels.Add(kv.Key / 100f);
            levels.Sort();
            return levels;
        }

        // ------------------------------------------------------------------ 기본 도형 · 머티리얼

        public static GameObject Prim(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 size,
            Material mat, Quaternion? rot = null, bool keepCollider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>폴더 안에 URP 머티리얼을 만들거나 불러옴 (이미 있으면 색 · 텍스처만 맞춤)</summary>
        public static Material Mat(string folder, string name, Color color, Texture tex = null,
            string shader = "Universal Render Pipeline/Simple Lit", bool cutout = false, bool transparent = false)
        {
            DataPaths.EnsureFolder(folder);
            string path = $"{folder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader)) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            if (cutout)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", 0f); // 아크릴 스탠드 · 포스터는 양면
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (transparent) ExternalArt.SetSurface(mat, true);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
