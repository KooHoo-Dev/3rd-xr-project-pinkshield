using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 정지 중 튜토리얼 대상 위로 내려오는 가짜 무대 조명 (FIX C2): 아래가 넓고 위로 갈수록 좁아지며 투명해지는 빛 원뿔.
    /// 실시간 조명은 쓰지 않고 정점 색 원뿔 메시 1개를 대상마다 크기만 바꿔 그립니다 (Sprites/Default: 항상 포함 셰이더, 양면).
    /// 정지 중 가게가 살짝 어두워지므로(TutorialLightDim) 대상만 무대 조명을 받는 느낌이 납니다.
    /// </summary>
    public sealed class TargetStageLight
    {
        const int Sides = 24;
        const float Height = 1.6f;      // 대상 바닥에서 원뿔 꼭대기까지 (m)
        const float TopRatio = 0.35f;   // 꼭대기 반지름 / 바닥 반지름
        const float BottomAlpha = 0.3f;

        readonly MeshRenderer[] cones;

        public TargetStageLight(Transform parent, Material material, int count)
        {
            Mesh mesh = BuildCone();
            cones = new MeshRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("StageLight" + i);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                cones[i] = r;
            }
        }

        /// <summary>i번 조명을 대상 bounds에 맞춰 놓음 (b가 null이면 끔)</summary>
        public void Place(int i, Bounds? b)
        {
            MeshRenderer r = cones[i];
            if (b == null) { if (r.enabled) r.enabled = false; return; }
            Bounds v = b.Value;
            float radius = Mathf.Max(v.extents.x, v.extents.z) * 1.3f + 0.05f;
            r.transform.SetPositionAndRotation(new Vector3(v.center.x, v.min.y, v.center.z), Quaternion.identity);
            r.transform.localScale = new Vector3(radius, Height, radius);
            if (!r.enabled) r.enabled = true;
        }

        public void HideAll()
        {
            for (int i = 0; i < cones.Length; i++) Place(i, null);
        }

        // 반지름 1 · 높이 1 원뿔대 옆면 (뚜껑 없음). 아래 알파 BottomAlpha → 위 0
        static Mesh BuildCone()
        {
            var verts = new Vector3[(Sides + 1) * 2];
            var colors = new Color[verts.Length];
            var tris = new int[Sides * 6];
            for (int s = 0; s <= Sides; s++)
            {
                float a = s * Mathf.PI * 2f / Sides;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[s * 2] = dir;
                verts[s * 2 + 1] = dir * TopRatio + Vector3.up;
                colors[s * 2] = new Color(1f, 1f, 1f, BottomAlpha);
                colors[s * 2 + 1] = new Color(1f, 1f, 1f, 0f);
            }
            for (int s = 0; s < Sides; s++)
            {
                int b = s * 2, t = s * 6;
                tris[t] = b; tris[t + 1] = b + 1; tris[t + 2] = b + 2;
                tris[t + 3] = b + 1; tris[t + 4] = b + 3; tris[t + 5] = b + 2;
            }
            var mesh = new Mesh { name = "StageLightCone", vertices = verts, colors = colors, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
