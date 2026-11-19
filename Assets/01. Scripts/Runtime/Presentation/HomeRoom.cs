using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardShop.Presentation
{
    /// <summary>
    /// 씬에 이미 있는 집 구역(ShopSceneBuilder의 "Home" 루트 · HomeInterior 프리팹) 찾기와, 런타임에 소품을 놓는 도구.
    /// 방 업그레이드(진열장 · 가구 · 택배)는 씬을 다시 빌드하지 않고 스스로 붙으므로 이름으로 찾습니다.
    /// 놓기 규칙은 에디터의 Placer와 같습니다: 모델 경계 상자의 align 점(각 축 -1 · 0 · 1)을 목표 위치에 맞춤.
    /// 경계 상자는 켜진 렌더러에서만 얻을 수 있으므로 집 실내가 보이는 동안(집 페이즈) 부르세요.
    /// </summary>
    public static class HomeRoom
    {
        public const string RootName = "Home";
        public const string InteriorName = "HomeInterior";

        /// <summary>씬 맨 위의 "Home" (없으면 null)</summary>
        public static Transform Root()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                foreach (GameObject go in SceneManager.GetSceneAt(i).GetRootGameObjects())
                    if (go.name == RootName) return go.transform;
            return null;
        }

        /// <summary>집 실내 프리팹 인스턴스 (집 페이즈에만 켜짐 — ZoneVisibility)</summary>
        public static Transform Interior()
        {
            Transform root = Root();
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
                if (root.GetChild(i).name.StartsWith(InteriorName)) return root.GetChild(i);
            return null;
        }

        /// <summary>꺼진 자식까지 이름으로 찾음 (깊이 우선)</summary>
        public static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        public static Bounds Bounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>
        /// prefab을 parent 아래 parent 로컬 좌표 target에 놓습니다 (경계 상자의 align 점 기준). 콜라이더는 지웁니다
        /// (장식이 레이 · 손을 가리지 않게).
        /// </summary>
        public static GameObject Place(Transform parent, GameObject prefab, Vector3 localTarget, Vector3 align, float yaw, float scale, string name)
        {
            if (prefab == null || parent == null) return null;
            GameObject go = Object.Instantiate(prefab, parent);
            go.name = name;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.Scale(prefab.transform.localScale, Vector3.one * scale);
            go.transform.localPosition = Vector3.zero;

            Bounds b = Bounds(go);
            Vector3 anchor = b.center + Vector3.Scale(b.extents, align);
            go.transform.position += parent.TransformPoint(localTarget) - anchor;
            RemoveColliders(go);
            return go;
        }

        /// <summary>original을 숨기고 그 자리 · 방향 · 크기 그대로 prefab을 놓습니다 (같은 묶음 모델끼리 피벗이 같다고 봄).</summary>
        public static GameObject Replace(Transform original, GameObject prefab, string name)
        {
            if (prefab == null || original == null) return null;
            GameObject go = Object.Instantiate(prefab, original.parent);
            go.name = name;
            go.transform.SetLocalPositionAndRotation(original.localPosition, original.localRotation);
            go.transform.localScale = original.localScale;
            RemoveColliders(go);
            original.gameObject.SetActive(false);
            return go;
        }

        public static void RemoveColliders(GameObject go)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        }

        /// <summary>빛 받는 단색 머티리얼 (base를 복제해 색만 바꿈 — 셰이더를 이름으로 찾지 않아 빌드에서 빠질 걱정 없음)</summary>
        public static Material Tinted(Material source, Color color)
        {
            if (source == null) return null;
            var m = new Material(source) { color = color };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            m.mainTexture = null;
            return m;
        }
    }
}
