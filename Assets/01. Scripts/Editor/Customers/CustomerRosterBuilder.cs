using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Cards.EditorTools;
using UnityEditor;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// CustomerRoster(손님 외형 목록)를 Toon City People 변형 프리팹(남 55 · 여 60)으로 채웁니다.
    /// 이미 분류(Local/Foreign)한 항목은 그대로 두고, 새 프리팹만 Local로 추가합니다.
    /// 원본(Sources) 프리팹은 숨은 부위 200개를 담고 있어 넣지 않습니다.
    /// 메뉴: Tools > CardShop > Customers > Build Customer Roster
    /// </summary>
    public static class CustomerRosterBuilder
    {
        public const string RosterPath = DataPaths.ConfigsDir + "/CustomerRoster.asset";
        const string TcpPrefabs = "Assets/99. External Assets/Toon City People/Prefabs";
        static readonly (string folder, CustomerGender gender)[] VariantFolders =
        {
            (TcpPrefabs + "/Male Character Variants", CustomerGender.Male),
            (TcpPrefabs + "/Female Character Variants", CustomerGender.Female),
        };

        [MenuItem("Tools/CardShop/Customers/Build Customer Roster", priority = 61)]
        public static CustomerRoster Build()
        {
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CustomerAnimatorBuilder.ControllerPath) == null)
                CustomerAnimatorBuilder.Build();

            CustomerRoster roster = LoadOrCreate();
            var known = new HashSet<GameObject>();
            roster.entries.RemoveAll(e => e.prefab == null);
            foreach (CustomerRoster.Entry e in roster.entries) known.Add(e.prefab);

            int added = 0;
            foreach (var (folder, gender) in VariantFolders)
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (prefab == null || known.Contains(prefab)) continue;
                    roster.entries.Add(new CustomerRoster.Entry { prefab = prefab, gender = gender, look = CustomerLook.Local });
                    added++;
                }
            roster.entries.Sort((a, b) => string.CompareOrdinal(a.prefab.name, b.prefab.name));

            RelinkControllers(roster);
            EditorUtility.SetDirty(roster);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CustomerRosterBuilder] {RosterPath}: 전체 {roster.entries.Count}명, 새로 추가 {added}명. " +
                      "외형 분류는 Tools > CardShop > Customers > Sort Customer Looks");
            return roster;
        }

        /// <summary>목록이 있으면 Animator 참조를 다시 연결합니다 (CustomerAnimatorBuilder가 호출).</summary>
        public static void RelinkControllers() => RelinkControllers(AssetDatabase.LoadAssetAtPath<CustomerRoster>(RosterPath));

        static void RelinkControllers(CustomerRoster roster)
        {
            if (roster == null) return;
            roster.maleController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CustomerAnimatorBuilder.ControllerPath);
            roster.femaleController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CustomerAnimatorBuilder.FemaleOverridePath);
            EditorUtility.SetDirty(roster);
        }

        static CustomerRoster LoadOrCreate()
        {
            var roster = AssetDatabase.LoadAssetAtPath<CustomerRoster>(RosterPath);
            if (roster != null) return roster;
            DataPaths.EnsureFolder(DataPaths.ConfigsDir);
            roster = ScriptableObject.CreateInstance<CustomerRoster>();
            AssetDatabase.CreateAsset(roster, RosterPath);
            return roster;
        }
    }
}
