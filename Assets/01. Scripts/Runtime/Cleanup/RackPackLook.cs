using CardShop.Interaction;
using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 진열대 꽂이 팩의 겉모습을 색 상자(큐브)에서 진짜 MALLANG 카드팩 모양으로 (11-21 피드백 P2 · V1).
    /// 씬을 다시 빌드하지 않아도 되게, 씬이 열리면 선반(ShelfController)의 팩 프리팹에서 "Visual"만 빌려 와 꽂이 팩마다 붙이고 큐브는 숨깁니다.
    /// 잡기 · 꽂기 · 충돌은 RackPack · HeldItem 그대로 (모양만 바뀜).
    /// </summary>
    public static class RackPackLook
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var shelf = Object.FindAnyObjectByType<ShelfController>();
            Transform source = shelf != null && shelf.PackPrefab != null ? shelf.PackPrefab.transform.Find("Visual") : null;
            if (source == null) return;
            foreach (RackPack pack in Object.FindObjectsByType<RackPack>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Dress(pack.transform, source);
        }

        static void Dress(Transform pack, Transform source)
        {
            if (pack.Find("PackLook") != null) return;
            Bounds cube = default;
            bool found = false;
            foreach (MeshRenderer r in pack.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.name.StartsWith("Visual_")) continue; // CleanupBuilder.Visual이 만든 색 상자
                if (!found) { cube = new Bounds(r.transform.localPosition, r.transform.localScale); found = true; }
                r.enabled = false;
            }
            if (!found) return;

            Transform look = Object.Instantiate(source, pack, false);
            look.name = "PackLook";
            // 팩 프리팹(0.075 × 0.13m)을 큐브 크기에 맞춤. 두께는 프리팹 그대로(얇은 봉지)
            float s = Mathf.Min(cube.size.x / 0.075f, cube.size.y / 0.13f);
            look.localPosition = cube.center;
            look.localRotation = Quaternion.identity;
            look.localScale = Vector3.one * s;
            foreach (Collider c in look.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        }
    }
}
