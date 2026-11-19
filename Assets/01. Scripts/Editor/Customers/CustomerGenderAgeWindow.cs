using CardShop.Cards;
using UnityEditor;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 손님 모델을 한 줄씩(이름 · 미리보기 · 성별 · 나이) 보며 성별 · 나이를 그 자리에서 고치는 창 (11-20 C1).
    /// 목소리 성별은 CustomerRoster 의 gender 에서 오므로, 틀린 줄을 눈으로 찾아 고칩니다. 값은 사람이 고릅니다.
    /// 메뉴: Tools > CardShop > Customers > Edit Customer Gender Age
    /// </summary>
    public sealed class CustomerGenderAgeWindow : EditorWindow
    {
        const float Thumb = 72f;

        CustomerRoster roster;
        Vector2 scroll;

        [MenuItem("Tools/CardShop/Customers/Edit Customer Gender Age", priority = 63)]
        static void Open() => GetWindow<CustomerGenderAgeWindow>("손님 성별·나이");

        void OnEnable() => roster = AssetDatabase.LoadAssetAtPath<CustomerRoster>(CustomerRosterBuilder.RosterPath);

        void OnInspectorUpdate()
        {
            // AssetPreview 는 비동기로 만들어지므로 그림이 준비될 때까지 다시 그립니다.
            if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
        }

        void OnGUI()
        {
            if (roster == null)
            {
                EditorGUILayout.HelpBox("CustomerRoster가 없습니다.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox("성별이 목소리 남녀를 정합니다. 그림과 다르면 바로 고치세요. 바꾸면 자동 저장됩니다.", MessageType.None);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < roster.entries.Count; i++) DrawRow(i, roster.entries[i]);
            EditorGUILayout.EndScrollView();
        }

        void DrawRow(int index, CustomerRoster.Entry e)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(Thumb));
            Texture2D tex = e.prefab != null ? AssetPreview.GetAssetPreview(e.prefab) : null;
            GUILayout.Label(tex, GUILayout.Width(Thumb), GUILayout.Height(Thumb));

            EditorGUILayout.BeginVertical();
            GUILayout.Label($"{index}. {(e.prefab != null ? e.prefab.name : "(없음)")}", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var gender = (CustomerGender)EditorGUILayout.EnumPopup("성별", e.gender);
            var age = (CustomerAge)EditorGUILayout.EnumPopup("나이", e.age);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(roster, "손님 성별·나이 변경");
                e.gender = gender;
                e.age = age;
                EditorUtility.SetDirty(roster);
                AssetDatabase.SaveAssetIfDirty(roster);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
    }
}
