using System.Collections.Generic;
using CardShop.Presentation;
using UnityEditor;
using UnityEngine;

namespace CardShop.Cards.EditorTools
{
    /// <summary>
    /// 집 업그레이드 단계 미리보기 (11-20 플레이 F5): Play 없이 씬 뷰에서 침대 크기 · 가구 자리를 눈으로 확인합니다.
    /// 게임과 같은 UpgradeFurniture로 놓으므로 모습이 같습니다. 놓은 모델은 씬에 저장되지 않고(DontSave),
    /// 바꿔 끼운 원래 가구는 씬 뷰에서만 숨깁니다(SceneVisibility — 씬 파일은 그대로). 씬에 놓은 단계 가구(Bed_Tier_n, SceneTiers)는
    /// 단계에 맞는 것만 켜고, [지우기] · 씬 저장 때 원래 켜짐 상태로 되돌립니다.
    /// 자리표(Map Edit 창 "집 가구: …")를 옮긴 뒤 다시 누르면 새 자리로 보입니다.
    /// </summary>
    public static class UpgradePreviewMenu
    {
        const string Menu = "Tools/CardShop/Home/Preview Upgrade Level/";
        const string Prefix = "Upgrade_";

        // 씬 Tier(Bed_Tier_n)는 켜고 끄기로 보여 주므로 원래 켜짐 상태를 기억했다가 되돌림. 미리보기 중 저장하면 먼저 되돌림
        static readonly Dictionary<GameObject, bool> tierStates = new Dictionary<GameObject, bool>();

        [InitializeOnLoadMethod]
        static void HookSave() => UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (_, __) => { if (previewing) Clear(HomeRoom.Interior()); };

        static bool previewing;

        [MenuItem(Menu + "1 (처음 방)")] static void Level1() => Show(1);
        [MenuItem(Menu + "2")] static void Level2() => Show(2);
        [MenuItem(Menu + "3 (모두 최고 단계)")] static void Level3() => Show(3);
        [MenuItem(Menu + "지우기")] static void ClearMenu() => Clear(HomeRoom.Interior());

        static void Show(int level)
        {
            Transform interior = HomeRoom.Interior();
            if (interior == null || !interior.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[UpgradePreview] 집 실내(Home/HomeInterior)가 없거나 꺼져 있습니다. 씬에서 켜고 다시 눌러 주세요 (경계 상자는 켜진 모델에서만 잼).");
                return;
            }
            Clear(interior);
            UpgradeCatalog catalog = UpgradeCatalog.Load();
            int count = 0;
            foreach (UpgradeCatalog.Track t in catalog.tracks)
            {
                if (t == null) continue;
                Dictionary<int, Transform> tiers = SceneTiers.Find(interior, t.kind);
                if (tiers.Count > 0)
                {
                    foreach (Transform tier in tiers.Values)
                        if (!tierStates.ContainsKey(tier.gameObject)) tierStates[tier.gameObject] = tier.gameObject.activeSelf;
                    SceneTiers.Apply(interior, t.kind, level);
                    count++;
                    continue;
                }
                var covered = new List<Transform>();
                List<GameObject> made = UpgradeFurniture.Build(interior, t, level, covered, UpgradeFurniture.FromScene);
                foreach (GameObject go in made)
                {
                    foreach (Transform child in go.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.DontSave;
                    count++;
                }
                // 원래 가구는 다시 켜고(씬 파일 그대로) 씬 뷰에서만 숨김
                foreach (Transform o in covered)
                {
                    o.gameObject.SetActive(true);
                    SceneVisibilityManager.instance.Hide(o.gameObject, true);
                }
            }
            previewing = true;
            SceneView.RepaintAll();
            Debug.Log($"[UpgradePreview] {level}단계 미리보기: 가구 {count}개 (씬에 저장되지 않음, 지우기로 되돌림)");
        }

        static void Clear(Transform interior)
        {
            if (interior == null) return;
            var old = new List<GameObject>();
            foreach (Transform t in interior.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(Prefix) && (t.gameObject.hideFlags & HideFlags.DontSave) != 0 && (t.parent == null || !t.parent.name.StartsWith(Prefix)))
                    old.Add(t.gameObject);
            foreach (GameObject go in old) Object.DestroyImmediate(go);
            foreach (KeyValuePair<GameObject, bool> e in tierStates) if (e.Key != null) e.Key.SetActive(e.Value);
            tierStates.Clear();
            if (previewing) SceneVisibilityManager.instance.Show(interior.gameObject, true);
            previewing = false;
            SceneView.RepaintAll();
        }
    }
}
