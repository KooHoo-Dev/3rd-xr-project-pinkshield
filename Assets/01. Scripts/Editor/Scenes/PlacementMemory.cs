using System.Collections.Generic;
using CardShop.Core;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardShop.EditorTools
{
    /// <summary>
    /// Build Shop Scene을 다시 실행해도 사람이 씬에서 옮긴 오브젝트 자리를 지킵니다 (2026-11-18 기기 테스트 피드백:
    /// "직접 배치해도, 배치·생성되는 오브젝트가 함께 따라오게").
    ///
    /// 1. <see cref="Capture"/>: 새 씬을 만들기 전, 기존 씬에서 <see cref="PlacementKey"/>가 붙은 오브젝트 중
    ///    빌더 기본 자리에서 옮겨진 것의 로컬 위치 · 회전 · 배율을 기억합니다.
    /// 2. 빌더가 새 씬을 다 만든 뒤 <see cref="Restore"/>: 새 오브젝트의 기본 자리를 기록하고, 기억한 것은 그 자리로 옮깁니다.
    ///
    /// 물건 자리 · 봉투 · 포스기 · 손님 지점처럼 게임 코드가 Transform을 읽는 것은 옮기면 실행 중 동작도 그대로 따라갑니다
    /// (CheckoutCounter는 물건 자리 Transform에, PaperBag은 자기 Transform 기준으로 담음).
    /// </summary>
    public static class PlacementMemory
    {
        public struct Pose
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }

        /// <summary>씬 파일에서 옮겨진 오브젝트 자리를 읽습니다. 씬이 없거나 표식이 없으면 빈 목록.</summary>
        public static Dictionary<string, Pose> Capture(string scenePath)
        {
            var moved = new Dictionary<string, Pose>();
            if (!System.IO.File.Exists(scenePath)) return moved;

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != scenePath) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            foreach (GameObject rootGo in scene.GetRootGameObjects())
                foreach (PlacementKey mark in rootGo.GetComponentsInChildren<PlacementKey>(true))
                {
                    if (string.IsNullOrEmpty(mark.key) || !mark.IsMoved) continue;
                    Transform t = mark.transform;
                    moved[mark.key] = new Pose { position = t.localPosition, rotation = t.localRotation, scale = t.localScale };
                }

            if (moved.Count > 0)
                Debug.Log($"[PlacementMemory] 손으로 옮긴 오브젝트 {moved.Count}개의 자리를 기억합니다: {string.Join(", ", moved.Keys)}");
            return moved;
        }

        /// <summary>새로 만든 씬의 표식마다 기본 자리를 기록하고, 기억해 둔 것은 그 자리로 되돌립니다.</summary>
        public static void Restore(Scene scene, Dictionary<string, Pose> moved)
        {
            var seen = new HashSet<string>();
            int restored = 0;
            foreach (GameObject rootGo in scene.GetRootGameObjects())
                foreach (PlacementKey mark in rootGo.GetComponentsInChildren<PlacementKey>(true))
                {
                    if (string.IsNullOrEmpty(mark.key)) continue;
                    if (!seen.Add(mark.key)) Debug.LogWarning($"[PlacementMemory] 같은 키가 두 번 있습니다: {mark.key}", mark);
                    mark.RecordDefault();
                    if (moved == null || !moved.TryGetValue(mark.key, out Pose p)) continue;
                    Transform t = mark.transform;
                    t.localPosition = p.position;
                    t.localRotation = p.rotation;
                    t.localScale = p.scale;
                    restored++;
                }

            if (restored > 0) Debug.Log($"[PlacementMemory] 손으로 옮긴 자리 {restored}개를 되살렸습니다.");
            if (moved != null)
                foreach (string key in moved.Keys)
                    if (!seen.Contains(key)) Debug.LogWarning($"[PlacementMemory] 기억한 '{key}'가 새 씬에 없어 자리를 되살리지 못했습니다.");
        }
    }
}
