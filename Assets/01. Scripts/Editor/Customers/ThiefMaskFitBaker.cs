using System.Collections.Generic;
using CardShop.Cards;
using UnityEditor;
using UnityEngine;

namespace CardShop.EditorTools.Customers
{
    /// <summary>
    /// 손님 모델마다 도둑 복면 자리를 재서 로스터에 넣음 (11-21 피드백 V2: 한 값(머리 뼈 위 0.09m)으로는 모델마다 위치가 달라 이상하게 씌워짐).
    /// 모델을 원점에 하나씩 꺼내, 머리 뼈(와 그 아래 뼈)에 붙은 정점만 모아 머리 범위를 잼:
    ///   - 크기 = 머리 폭 ÷ 모든 모델 머리 폭의 중간값 (사용자가 맞춘 복면 프리팹 크기가 "보통 머리" 기준이 되게)
    ///   - 자리 = 복면 윗면이 정수리보다 1cm 위, 복면 앞면이 얼굴 앞면보다 1cm 앞, 좌우는 머리 가운데
    ///     (앞뒤를 머리 가운데로 잡았더니 묶은 머리가 긴 모델은 가운데가 뒤로 밀려 얼굴이 복면 밖으로 나왔음)
    /// 결과는 CustomerRoster.Entry.maskOffset · maskScale. 이상한 모델만 로스터 인스펙터에서 손으로 고치면 됩니다(다시 구우면 덮어씀).
    /// </summary>
    public static class ThiefMaskFitBaker
    {
        const string RosterPath = "Assets/02. Data/Configs/CustomerRoster.asset";
        const string MaskPath = "Assets/03. Prefabs/Customers/ThiefMask.prefab";
        const float TopGap = 0.01f;

        struct Fit { public CustomerRoster.Entry Entry; public Vector3 HeadPos; public Bounds Head; }

        [MenuItem("Tools/CardShop/Customers/Bake Thief Mask Fit", priority = 64)]
        public static void Bake()
        {
            var roster = AssetDatabase.LoadAssetAtPath<CustomerRoster>(RosterPath);
            var mask = AssetDatabase.LoadAssetAtPath<GameObject>(MaskPath);
            if (roster == null || mask == null) { Debug.LogError($"[ThiefMaskFit] {RosterPath} 또는 {MaskPath} 없음"); return; }
            Bounds maskBounds = MaskBounds(mask);
            float maskTop = maskBounds.max.y * mask.transform.localScale.y;
            float maskFront = maskBounds.max.z * mask.transform.localScale.z;

            var fits = new List<Fit>();
            int failed = 0;
            foreach (CustomerRoster.Entry e in roster.entries)
            {
                if (e?.prefab == null) continue;
                if (Measure(e.prefab, out Vector3 headPos, out Bounds head)) fits.Add(new Fit { Entry = e, HeadPos = headPos, Head = head });
                else { failed++; e.maskScale = 0f; }
            }
            if (fits.Count == 0) { Debug.LogError("[ThiefMaskFit] 잰 모델이 없음"); return; }

            var widths = new List<float>();
            foreach (Fit f in fits) widths.Add(f.Head.size.x);
            widths.Sort();
            float median = widths[widths.Count / 2];

            foreach (Fit f in fits)
            {
                float s = Mathf.Clamp(f.Head.size.x / median, 0.8f, 1.25f);
                var center = new Vector3(f.Head.center.x, f.Head.max.y + TopGap - maskTop * s, f.Head.max.z + TopGap - maskFront * s);
                f.Entry.maskOffset = center - f.HeadPos;
                f.Entry.maskScale = s;
            }
            EditorUtility.SetDirty(roster);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ThiefMaskFit] {fits.Count}명 잼, 실패 {failed}명 (예전 방식), 머리 폭 중간값 {median:0.000}m");
        }

        /// <summary>배치 모드: -executeMethod CardShop.EditorTools.Customers.ThiefMaskFitBaker.BakeFromCommandLine</summary>
        public static void BakeFromCommandLine()
        {
            Bake();
            EditorApplication.Exit(0);
        }

        // 원점 · 기본 자세에서 머리 뼈 위치와, 머리에 붙은 정점 범위 (모델 루트 = 월드)
        static bool Measure(GameObject prefab, out Vector3 headPos, out Bounds head)
        {
            headPos = default;
            head = default;
            GameObject go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                go.transform.localScale = Vector3.one;
                var animator = go.GetComponentInChildren<Animator>();
                Transform headBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                if (headBone == null) headBone = FindByName(go.transform, "head");
                if (headBone == null) return false;
                headPos = headBone.position;

                bool any = false;
                var baked = new Mesh();
                foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Mesh mesh = smr.sharedMesh;
                    if (mesh == null) continue;
                    Transform[] bones = smr.bones;
                    BoneWeight[] weights = mesh.boneWeights;
                    if (weights == null || weights.Length != mesh.vertexCount) continue;
                    var isHead = new bool[bones.Length];
                    for (int b = 0; b < bones.Length; b++) isHead[b] = bones[b] != null && (bones[b] == headBone || bones[b].IsChildOf(headBone));

                    smr.BakeMesh(baked, true);
                    Vector3[] v = baked.vertices;
                    Matrix4x4 toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                    for (int i = 0; i < v.Length && i < weights.Length; i++)
                    {
                        BoneWeight w = weights[i];
                        if (w.boneIndex0 >= isHead.Length || !isHead[w.boneIndex0] || w.weight0 < 0.5f) continue;
                        Vector3 p = toWorld.MultiplyPoint3x4(v[i]);
                        if (!any) { head = new Bounds(p, Vector3.zero); any = true; }
                        else head.Encapsulate(p);
                    }
                }
                Object.DestroyImmediate(baked);
                // 머리로 보기엔 너무 작거나 큰 것(뼈 가중치가 이상한 모델)은 예전 방식으로
                return any && head.size.x > 0.1f && head.size.x < 0.45f && head.size.y > 0.1f;
            }
            finally { Object.DestroyImmediate(go); }
        }

        static Transform FindByName(Transform root, string part)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLowerInvariant().EndsWith(part)) return t;
            return null;
        }

        // 복면 프리팹 루트 기준 범위 (머리 가운데 = 원점, +Z = 얼굴 앞)
        static Bounds MaskBounds(GameObject mask)
        {
            var filter = mask.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return new Bounds(Vector3.zero, new Vector3(0.26f, 0.2f, 0.26f));
            Bounds local = filter.sharedMesh.bounds;
            Matrix4x4 m = mask.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var b = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? -1 : 1, (i / 2) % 2 == 0 ? -1 : 1, i / 4 == 0 ? -1 : 1);
                b.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, corner)));
            }
            return b;
        }
    }
}
