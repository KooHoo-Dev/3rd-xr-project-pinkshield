using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 카드 더미 아래쪽 덩어리의 생김새 (FIX D3: 회백색 정육면체가 "네모 상자"처럼 보임).
    /// 옆면 = 카드가 겹친 가로 줄무늬(코드로 만든 4px 텍스처를 카드 한 장에 한 번씩 반복), 윗면 = 카드 뒷면 그림.
    /// 메시는 1×1×1 상자(로컬 Z = 두께, +Z가 아래)라 CounterPileView가 크기만 정해 줍니다. 바닥면은 카운터에 닿아 안 보여서 없음.
    /// </summary>
    public sealed class CardPileBlockLook : MonoBehaviour
    {
        static Mesh mesh;
        static Texture2D stripes;

        Material side;
        Material top;

        /// <param name="edge">카드 옆면 머티리얼 (CardEdge, 없으면 URP 기본)</param>
        /// <param name="back">카드 뒷면 머티리얼 (CardBack, 없으면 옆면과 같게)</param>
        public static CardPileBlockLook Create(Transform parent, Material edge, Material back)
        {
            var go = new GameObject("BoxPileBlock");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Mesh();
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var look = go.AddComponent<CardPileBlockLook>();
            look.side = edge != null ? new Material(edge) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            look.side.SetTexture("_BaseMap", Stripes());
            look.top = back != null ? back : look.side;
            r.sharedMaterials = new[] { look.side, look.top };
            return look;
        }

        /// <summary>쌓인 장수만큼 줄무늬를 반복 (덩어리 높이 / 카드 두께)</summary>
        public void Fit(float cardsTall) => side.SetTextureScale("_BaseMap", new Vector2(1f, Mathf.Max(1f, cardsTall)));

        void OnDestroy()
        {
            if (side != null) Destroy(side);
        }

        // 카드 한 장 = 밝은 옆면 3px + 어두운 틈 1px
        static Texture2D Stripes()
        {
            if (stripes != null) return stripes;
            stripes = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "PileStripes" };
            var light = new Color32(255, 255, 255, 255);
            stripes.SetPixels32(new[] { new Color32(150, 145, 140, 255), light, light, light });
            stripes.Apply(false, true);
            return stripes;
        }

        // 옆면 4개(서브메시 0, v = 두께 방향 0~1) + 윗면(서브메시 1, z = -0.5)
        static Mesh Mesh()
        {
            if (mesh != null) return mesh;
            var v = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var sides = new System.Collections.Generic.List<int>();
            Vector2[] ring = { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) };
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % 4];
                int s = v.Count;
                v.Add(new Vector3(a.x, a.y, -0.5f)); uv.Add(new Vector2(0f, 1f));
                v.Add(new Vector3(b.x, b.y, -0.5f)); uv.Add(new Vector2(1f, 1f));
                v.Add(new Vector3(b.x, b.y, 0.5f)); uv.Add(new Vector2(1f, 0f));
                v.Add(new Vector3(a.x, a.y, 0.5f)); uv.Add(new Vector2(0f, 0f));
                sides.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 }); // 바깥에서 볼 때 시계 방향 (Unity 앞면)
            }
            int t = v.Count;
            for (int i = 0; i < 4; i++) { v.Add(new Vector3(ring[i].x, ring[i].y, -0.5f)); uv.Add(ring[i] + new Vector2(0.5f, 0.5f)); }
            var topTris = new[] { t, t + 2, t + 1, t, t + 3, t + 2 }; // 위(-Z)에서 볼 때 시계 방향

            mesh = new Mesh { name = "CardPileBlock" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(sides, 0);
            mesh.SetTriangles(topTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
