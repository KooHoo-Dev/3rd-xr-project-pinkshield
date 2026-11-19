using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardShop.Core
{
    /// <summary>
    /// 씬 계층 경로("Root/Child/Grandchild") 만들기 · 찾기 (맵_수정_도구_설계 5장).
    /// 빌더가 이름표(PlacementKey)를 붙이지 않은 오브젝트도, 맵 수정 도구에서 손으로 이름표를 붙이면
    /// Build Shop Scene 재실행 뒤 같은 경로의 오브젝트를 찾아 자리를 되살립니다. GameObject.Find와 달리 꺼진 것도 찾습니다.
    /// </summary>
    public static class HierarchyPath
    {
        public static string Of(Transform t)
        {
            if (t == null) return null;
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        /// <summary>경로가 같은 첫 오브젝트. 없으면 null</summary>
        public static Transform Find(Scene scene, string path)
        {
            if (string.IsNullOrEmpty(path) || !scene.IsValid()) return null;
            string[] parts = path.Split('/');
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                Transform t = Walk(root.transform, parts, 1);
                if (t != null) return t;
            }
            return null;
        }

        static Transform Walk(Transform t, string[] parts, int index)
        {
            if (index == parts.Length) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform c = t.GetChild(i);
                if (c.name != parts[index]) continue;
                Transform found = Walk(c, parts, index + 1);
                if (found != null) return found;
            }
            return null;
        }
    }
}
