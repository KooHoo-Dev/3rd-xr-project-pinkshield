using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 가게 벽장(계산대 뒤 BackShelf_0 ~ 6 · 왼쪽 LeftShelf_0 · 1) 안으로 몸이 들어가지 못하게 막는 보이지 않는 상자 (11-22 C1:
    /// 벽장 뒤로 걸어가면 그냥 통과함). 벽장은 콜라이더 없이 놓여 있었음 (InteriorBuilder — 서랍장 · 진열대만 상자 콜라이더).
    /// 침대 막기(BedBodyBlock)와 같은 방식: Ignore Raycast 층이라 레이 · 손은 그대로, 벽장마다 모델 범위로 상자. 씬 · 프리팹 수정 없음.
    /// </summary>
    public sealed class ShelfBodyBlock : MonoBehaviour
    {
        const int IgnoreRaycastLayer = 2;
        static readonly string[] Prefixes = { "BackShelf_", "LeftShelf_" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (IsShelf(t.name) && t.GetComponent<ShelfBodyBlock>() == null && t.GetComponentInChildren<Collider>(true) == null)
                    t.gameObject.AddComponent<ShelfBodyBlock>();
        }

        static bool IsShelf(string name)
        {
            foreach (string p in Prefixes)
                if (name.StartsWith(p)) return true;
            return false;
        }

        void Start()
        {
            bool any = false;
            Bounds b = default;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) return;

            // 월드 축에 맞춘 상자 (벽장은 0° · 90°로만 놓임): 부모 회전 · 크기와 상관없게 월드에 그대로 둠
            var go = new GameObject("ShelfBodyBlock") { layer = IgnoreRaycastLayer };
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(b.center, Quaternion.identity);
            Vector3 s = go.transform.lossyScale;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(Safe(b.size.x, s.x), Safe(b.size.y, s.y), Safe(b.size.z, s.z));
        }

        static float Safe(float world, float scale) => Mathf.Abs(scale) > 1e-4f ? world / Mathf.Abs(scale) : world;
    }
}
