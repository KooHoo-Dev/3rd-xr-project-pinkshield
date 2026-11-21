using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 바닥에 깔린 매트 · 러그의 윗면 높이 (11-22 T1 · T2). 매트 · 러그는 콜라이더가 없어 레이가 그 밑 바닥에 닿으므로,
    /// 그 자리에 놓은 쓰레기 · 도둑이 떨군 팩이 매트 속에 묻혀 안 보였음. 이름이 DoorMat · Rug로 시작하는 물건의 그려지는 범위로 잽니다.
    /// 덮개 목록은 10초마다 다시 찾습니다 — 집 러그가 업그레이드로 바뀌어도 따라감.
    /// </summary>
    public static class FloorSurface
    {
        const float Above = 0.005f;      // 덮개 윗면에서 이만큼 위 (m)
        const float RescanSeconds = 10f;

        static readonly System.Collections.Generic.List<Renderer> covers = new System.Collections.Generic.List<Renderer>();
        static float scannedAt = float.NegativeInfinity;

        /// <summary>(x, z) 자리의 바닥 높이: 매트 · 러그가 있으면 그 윗면, 없으면 floorY</summary>
        public static float TopAt(Vector3 p, float floorY)
        {
            if (Time.unscaledTime - scannedAt > RescanSeconds || covers.Exists(r => r == null)) Scan();
            float top = floorY;
            foreach (Renderer r in covers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds b = r.bounds;
                if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
                if (b.max.y > top && b.max.y < floorY + 0.1f) top = b.max.y; // 10cm 넘게 높은 것은 덮개가 아님
            }
            return top;
        }

        static void Scan()
        {
            scannedAt = Time.unscaledTime;
            covers.Clear();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (IsCover(r.transform)) covers.Add(r);
        }

        static bool IsCover(Transform t)
        {
            for (int i = 0; i < 3 && t != null; i++, t = t.parent)
                if (t.name.StartsWith("DoorMat") || t.name.StartsWith("Rug")) return true;
            return false;
        }

        /// <summary>꺼져 있어도 쓸 수 있는 메시 범위(월드). Renderer.bounds는 꺼진 물건에서 비어 있음</summary>
        public static bool MeshBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds m = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = m.center + Vector3.Scale(m.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 w = mf.transform.TransformPoint(corner);
                    if (!any) { bounds = new Bounds(w, Vector3.zero); any = true; }
                    else bounds.Encapsulate(w);
                }
            }
            return any;
        }

        /// <summary>물건의 아랫면이 그 자리 바닥(덮개 포함) 바로 위에 오게 올림. 내리지는 않음(가구 위 물건 그대로)</summary>
        public static void LiftAboveFloor(GameObject go, float floorY = 0f)
        {
            if (!MeshBounds(go, out Bounds b)) return;
            float ground = TopAt(b.center, floorY) + Above;
            if (b.min.y < ground) go.transform.position += Vector3.up * (ground - b.min.y);
        }
    }
}
