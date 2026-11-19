using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 대상 Renderer에 외곽선 재질(CardShop/Outline, 뒤집은 껍데기)을 잠깐 덧붙이고 떼는 일 (FIX C2).
    /// 카드 · 종이처럼 아주 얇은 것, 정적 배칭된 것, 글자 · 선 · 파티클은 외곽선이 안 보이거나 깨지므로 건너뜁니다 →
    /// 외곽선을 하나도 못 붙인 대상은 TargetHighlighter가 예전 선 상자로 대신 그립니다.
    /// 뗄 때는 저장해 둔 배열로 되돌리지 않고 외곽선 재질만 빼서, 그동안 다른 코드가 바꾼 재질을 지우지 않습니다.
    /// </summary>
    public sealed class OutlineMarker
    {
        const float MinThickness = 0.004f; // 이보다 얇은 물건은 껍데기가 안 보임 (m)

        readonly Material material; // 셰이더를 못 찾으면 null → 아무것도 하지 않음
        readonly List<Renderer> marked = new List<Renderer>();

        public OutlineMarker()
        {
            Shader shader = Resources.Load<Shader>("CardShopOutline");
            if (shader != null && shader.isSupported) material = new Material(shader) { name = "TutorialOutline" };
        }

        /// <summary>두께(m) · 색을 바꿈 (맥박). 모든 대상이 같은 재질을 씀</summary>
        public void Paint(Color color, float width)
        {
            if (material == null) return;
            material.SetColor("_Color", color);
            material.SetFloat("_Width", width);
        }

        /// <summary>대상의 Renderer에 외곽선을 붙임. 하나라도 붙였으면 true</summary>
        public bool Mark(HighlightTarget target)
        {
            if (material == null || target == null) return false;
            bool any = false;
            foreach (Renderer r in target.Renderers)
            {
                if (!CanOutline(r)) continue;
                if (!marked.Contains(r))
                {
                    // shortcut: 서브메시가 여러 개면 덧붙인 재질은 마지막 서브메시에만 그려짐, 외곽선이 일부만 보이면 자식 메시 복제로 바꿀 것
                    Material[] mats = r.sharedMaterials;
                    var withOutline = new Material[mats.Length + 1];
                    mats.CopyTo(withOutline, 0);
                    withOutline[mats.Length] = material;
                    r.sharedMaterials = withOutline;
                    marked.Add(r);
                }
                any = true;
            }
            return any;
        }

        public void Clear()
        {
            foreach (Renderer r in marked)
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                var kept = new List<Material>(mats.Length);
                foreach (Material m in mats) if (m != material) kept.Add(m);
                r.sharedMaterials = kept.ToArray();
            }
            marked.Clear();
        }

        public void Dispose()
        {
            Clear();
            if (material != null) Object.Destroy(material);
        }

        static bool CanOutline(Renderer r)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.isPartOfStaticBatch) return false;
            if (r.GetComponent<TMP_Text>() != null) return false;
            Bounds local;
            if (r is SkinnedMeshRenderer sk) local = sk.localBounds;
            else if (r is MeshRenderer && r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null) local = f.sharedMesh.bounds;
            else return false;
            Vector3 size = Vector3.Scale(local.size, r.transform.lossyScale);
            return Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) >= MinThickness;
        }
    }
}
