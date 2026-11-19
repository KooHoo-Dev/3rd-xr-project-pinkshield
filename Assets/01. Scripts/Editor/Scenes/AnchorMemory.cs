using System;
using System.Collections.Generic;
using CardShop.Core;
using CardShop.Presentation;
using CardShop.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardShop.EditorTools
{
    /// <summary>
    /// Build Shop Scene은 씬을 새로 만들므로, 빌더가 만들지 않는 자리표(PlacementAnchor · CardCaseAnchor · TutorialMemoAnchor)는
    /// 그대로 두면 사라집니다 (맵_수정_도구_설계 5장). 새 씬을 만들기 전에 자리표를 적어 두었다가, 다 만든 뒤 같은 자리에 다시 만듭니다.
    /// <see cref="PlacementMemory"/>가 "빌더가 만든 물건의 옮긴 자리"를 지킨다면, 이 클래스는 "사람이 더한 자리표 자체"를 지킵니다.
    /// </summary>
    public static class AnchorMemory
    {
        public struct Saved
        {
            public Type type;
            public string name;
            public string parentPath; // 없으면 씬 맨 위
            public Vector3 position;
            public Quaternion rotation;
            public string key;
            public Vector3 previewCenter, previewSize;
        }

        static readonly Type[] AnchorTypes = { typeof(PlacementAnchor), typeof(CardCaseAnchor), typeof(TutorialMemoAnchor) };

        /// <summary>지금 열린 씬(PlacementMemory.Capture가 연 씬)의 자리표를 적어 둡니다.</summary>
        public static List<Saved> Capture(Scene scene)
        {
            var list = new List<Saved>();
            if (!scene.IsValid()) return list;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Type type in AnchorTypes)
                    foreach (Component c in root.GetComponentsInChildren(type, true))
                    {
                        Transform t = c.transform;
                        var s = new Saved
                        {
                            type = type, name = t.name, parentPath = HierarchyPath.Of(t.parent),
                            position = t.position, rotation = t.rotation,
                        };
                        if (c is PlacementAnchor a) { s.key = a.key; s.previewCenter = a.previewCenter; s.previewSize = a.previewSize; }
                        list.Add(s);
                    }
            if (list.Count > 0) Debug.Log($"[AnchorMemory] 자리표 {list.Count}개를 기억합니다.");
            return list;
        }

        /// <summary>새 씬에 자리표를 다시 만듭니다. 부모가 없어졌으면 씬 맨 위에.</summary>
        public static void Restore(Scene scene, List<Saved> saved)
        {
            if (saved == null) return;
            foreach (Saved s in saved)
            {
                var go = new GameObject(s.name);
                SceneManager.MoveGameObjectToScene(go, scene);
                Transform parent = HierarchyPath.Find(scene, s.parentPath);
                if (parent != null) go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(s.position, s.rotation);
                Component c = go.AddComponent(s.type);
                if (c is PlacementAnchor a) { a.key = s.key; a.previewCenter = s.previewCenter; a.previewSize = s.previewSize; }
            }
            if (saved.Count > 0) Debug.Log($"[AnchorMemory] 자리표 {saved.Count}개를 되살렸습니다.");
        }
    }
}
