using System;
using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 업그레이드 단계의 가구 모델 놓기 (UpgradeCatalog.Visual). RoomUpgradeView(게임)와 에디터 미리보기 메뉴
    /// (Tools > CardShop > Home > Preview Upgrade Level)가 함께 씁니다 — 미리보기와 게임 모습이 같도록 한 곳에서.
    /// - 새로 놓는 가구: 씬에 자리표 "Home/Upgrade/분야_단계_번호"가 있으면 그 자리 · 방향, 없으면 카탈로그 좌표 (F7)
    /// - 바꿔 놓는 가구(replaces): 원래 가구 자리에 (크기 · 모서리 맞춤 · 옮김, F5). 원래 가구를 옮기면 따라가므로 자리표 없음
    /// </summary>
    public static class UpgradeFurniture
    {
        public const string AnchorPrefix = "Home/Upgrade/";

        public static string AnchorKey(UpgradeKind kind, int level, int index) => $"{AnchorPrefix}{kind}_{level}_{index}";

        /// <summary>자리표 없이 놓을 때의 자리 (집 실내 기준 카탈로그 좌표 → 월드). 자리표 기준점 = 모델 경계 상자의 align 점</summary>
        public static Pose DefaultPose(Transform interior, UpgradeCatalog.Visual vis) =>
            new Pose(interior.TransformPoint(vis.position), interior.rotation * Quaternion.Euler(0f, vis.yaw, 0f));

        /// <summary>자리표로 옮길 수 있는 가구인가 (새로 놓는 가구만)</summary>
        public static bool IsAnchored(UpgradeCatalog.Visual vis) => vis != null && string.IsNullOrEmpty(vis.replaces);

        /// <summary>
        /// 1 ~ level 단계의 모델을 차례로 놓고 만든 것을 돌려줍니다 (나중 단계가 같은 가구를 바꾸면 앞 단계 모델은 지움).
        /// 숨긴 원래 가구는 covered에 더합니다 (되돌릴 때 다시 켜기). anchorAt: 자리표 키 → 자리 (없으면 null).
        /// </summary>
        public static List<GameObject> Build(Transform interior, UpgradeCatalog.Track track, int level, List<Transform> covered,
            Func<string, Pose?> anchorAt)
        {
            var made = new List<GameObject>();
            var replacedBy = new Dictionary<string, GameObject>();
            if (interior == null || track?.levels == null) return made;

            for (int lv = 2; lv <= level && lv - 2 < track.levels.Length; lv++)
            {
                UpgradeCatalog.Level entry = track.levels[lv - 2];
                if (entry?.visuals == null) continue;
                for (int v = 0; v < entry.visuals.Length; v++)
                {
                    UpgradeCatalog.Visual vis = entry.visuals[v];
                    if (vis == null) continue;
                    string name = $"Upgrade_{track.kind}_{lv}_{v}";
                    GameObject go = IsAnchored(vis)
                        ? PlaceNew(interior, vis, name, anchorAt?.Invoke(AnchorKey(track.kind, lv, v)))
                        : ReplaceOriginal(interior, vis, name, covered, replacedBy, made);
                    if (go == null) continue;
                    if (vis.addLight) AddLamp(go);
                    made.Add(go);
                }
            }
            return made;
        }

        static GameObject PlaceNew(Transform interior, UpgradeCatalog.Visual vis, string name, Pose? anchored)
        {
            if (vis.model == null) return null;
            Vector3 local = vis.position;
            float yaw = vis.yaw;
            if (anchored.HasValue)
            {
                local = interior.InverseTransformPoint(anchored.Value.position);
                yaw = (Quaternion.Inverse(interior.rotation) * anchored.Value.rotation).eulerAngles.y;
            }
            return HomeRoom.Place(interior, vis.model, local, vis.align, yaw, vis.Scale3, name);
        }

        static GameObject ReplaceOriginal(Transform interior, UpgradeCatalog.Visual vis, string name,
            List<Transform> covered, Dictionary<string, GameObject> replacedBy, List<GameObject> made)
        {
            if (vis.model == null) return null; // 모델을 못 찾음 (Build Upgrade Catalog)
            if (replacedBy.TryGetValue(vis.replaces, out GameObject prev)) { made.Remove(prev); HomeRoom.Kill(prev); }
            Transform original = HomeRoom.FindDeep(interior, vis.replaces);
            if (original == null) return null;
            if (!covered.Contains(original)) covered.Add(original);
            original.gameObject.SetActive(true); // 자리 · 크기를 읽기 위해 (Replace가 다시 숨김)
            GameObject go = HomeRoom.Replace(original, vis.model, name, vis.Scale3, vis.matchCorner ? vis.align : (Vector3?)null);
            if (go != null) replacedBy[vis.replaces] = go;
            return go;
        }

        static void AddLamp(GameObject lamp)
        {
            Bounds b = HomeRoom.Bounds(lamp);
            var go = new GameObject("LampLight");
            go.transform.SetParent(lamp.transform, true);
            go.transform.position = new Vector3(b.center.x, b.max.y - 0.1f, b.center.z);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.55f);
            light.intensity = 1.2f;
            light.range = 2.5f;
            light.shadows = LightShadows.None;
        }

        /// <summary>씬의 자리표에서 자리 찾기 (게임 · 미리보기 공통)</summary>
        public static Pose? FromScene(string key)
        {
            PlacementAnchor a = PlacementAnchor.Get(key);
            return a != null ? new Pose(a.transform.position, a.transform.rotation) : (Pose?)null;
        }
    }
}
