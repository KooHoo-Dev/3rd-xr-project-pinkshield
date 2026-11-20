using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Presentation;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 맵 수정 도구에 집 업그레이드 가구 자리표를 더합니다 (11-20 플레이 F7). 고정 목록(MapAnchorCatalog) 대신 카탈로그에서 만듦:
    /// 새로 놓는 가구(노트북 · 조명 · 포스터 · 피규어 · 에어컨 등)마다 "Home/Upgrade/분야_단계_번호". 바꿔 끼우는 가구(침대 · 러그)는
    /// 원래 가구를 옮기면 따라가므로 넣지 않습니다. 미리보기 상자 = 그 모델 크기 (모델마다 한 번 재고 기억).
    /// </summary>
    public static class UpgradeAnchorEntries
    {
        static readonly Dictionary<GameObject, (Vector3 center, Vector3 size)> sizes = new Dictionary<GameObject, (Vector3, Vector3)>();
        static List<MapAnchorCatalog.Entry> cached;

        /// <summary>카탈로그가 바뀌었을 수 있을 때 (Build Upgrade Catalog 뒤) 다시 만들게 함</summary>
        public static void Invalidate() => cached = null;

        public static IReadOnlyList<MapAnchorCatalog.Entry> All()
        {
            if (cached != null) return cached;
            cached = new List<MapAnchorCatalog.Entry>();
            UpgradeCatalog catalog = UpgradeCatalog.Load();
            if (catalog.tracks == null) return cached;
            foreach (UpgradeCatalog.Track t in catalog.tracks)
            {
                if (t?.levels == null) continue;
                for (int i = 0; i < t.levels.Length; i++)
                {
                    UpgradeCatalog.Visual[] visuals = t.levels[i]?.visuals;
                    if (visuals == null) continue;
                    int level = i + 2;
                    for (int v = 0; v < visuals.Length; v++)
                    {
                        UpgradeCatalog.Visual vis = visuals[v];
                        if (!UpgradeFurniture.IsAnchored(vis) || vis.model == null) continue;
                        (Vector3 center, Vector3 size) = Preview(vis);
                        cached.Add(new MapAnchorCatalog.Entry
                        {
                            key = UpgradeFurniture.AnchorKey(t.kind, level, v),
                            label = $"집 가구: {t.displayName} {level}단계" + (visuals.Length > 1 ? $" ({v + 1})" : ""),
                            defaultPose = () => HomeRoom.Interior() is Transform home ? UpgradeFurniture.DefaultPose(home, vis) : (Pose?)null,
                            previewCenter = center,
                            previewSize = size,
                        });
                    }
                }
            }
            return cached;
        }

        // 자리표 기준점 = 모델 경계 상자의 align 점 → 상자 가운데는 그 반대쪽으로 반 크기만큼 (자리표 로컬 = 모델 방향)
        static (Vector3, Vector3) Preview(UpgradeCatalog.Visual vis)
        {
            if (!sizes.TryGetValue(vis.model, out (Vector3 center, Vector3 size) s))
            {
                GameObject temp = Object.Instantiate(vis.model);
                temp.hideFlags = HideFlags.HideAndDontSave;
                temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Bounds b = HomeRoom.Bounds(temp);
                s = (b.center, b.size);
                Object.DestroyImmediate(temp);
                sizes[vis.model] = s;
            }
            Vector3 size = Vector3.Scale(s.size, vis.Scale3);
            // align은 월드 축 기준이라 yaw만큼 되돌려 자리표 로컬 축으로
            Vector3 alignLocal = Quaternion.Euler(0f, -vis.yaw, 0f) * vis.align;
            Vector3 center = -Vector3.Scale(size * 0.5f, alignLocal);
            return (center, size);
        }
    }
}
