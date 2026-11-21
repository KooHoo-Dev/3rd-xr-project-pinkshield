using CardShop.Interaction;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 침대 위로 몸이 올라가지 못하게 막는 보이지 않는 상자 (11-21 2차 H5: 침대에 올라서면 어지러움).
    /// 쓰레기통(TrashBin.climbBlocker, BUG-021)과 같은 방식: Ignore Raycast 층의 상자, 높이 1.6m.
    /// 씬의 침대 단계(Bed_Tier_0 · 1 · 2)마다 스스로 붙고, 그 단계가 처음 켜질 때 모델 범위로 상자를 만듭니다 (꺼진 모델은 범위를 모름).
    /// 잠자기 스위치(벽) 같은 누르는 버튼이 상자 안에 들어가면 버튼 아래까지만 높여 레이를 가리지 않게 합니다. 씬 수정 없음.
    /// </summary>
    public sealed class BedBodyBlock : MonoBehaviour
    {
        const float Height = 1.6f;
        const float MinAboveBed = 0.3f; // 버튼 때문에 낮추더라도 침대 윗면보다 이만큼은 (올라서지 못하게)
        const int IgnoreRaycastLayer = 2;

        bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.StartsWith("Bed_Tier_") && t.GetComponent<BedBodyBlock>() == null)
                    t.gameObject.AddComponent<BedBodyBlock>();
        }

        void OnEnable()
        {
            if (built) return;
            built = true;
            if (!TryBounds(out Bounds b)) return;

            float top = b.min.y + Height;
            foreach (PokeButton button in FindObjectsByType<PokeButton>(FindObjectsSortMode.None))
            {
                Vector3 p = button.transform.position;
                if (p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z && p.y < top)
                    top = Mathf.Max(b.max.y + MinAboveBed, p.y - 0.1f);
            }

            var go = new GameObject("BedBodyBlock") { layer = IgnoreRaycastLayer };
            go.transform.SetParent(transform, true);
            go.transform.SetPositionAndRotation(new Vector3(b.center.x, b.min.y, b.center.z), Quaternion.identity);
            Vector3 s = go.transform.lossyScale;
            var box = go.AddComponent<BoxCollider>();
            float h = top - b.min.y;
            box.size = new Vector3(Safe(b.size.x, s.x), Safe(h, s.y), Safe(b.size.z, s.z));
            box.center = new Vector3(0f, Safe(h, s.y) / 2f, 0f);
        }

        bool TryBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        static float Safe(float world, float scale) => Mathf.Abs(scale) > 1e-4f ? world / Mathf.Abs(scale) : world;
    }
}
