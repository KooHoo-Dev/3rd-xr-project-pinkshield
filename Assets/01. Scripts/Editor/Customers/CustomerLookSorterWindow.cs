using CardShop.Cards;
using UnityEditor;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 손님 외형을 썸네일로 보며 Local(아시아계) / Foreign(외국인)으로 나누는 창.
    /// 외형 판단은 자동으로 할 수 없어 사람이 눈으로 정합니다. 썸네일을 클릭하면 분류가 바뀝니다.
    /// 메뉴: Tools > CardShop > Customers > Sort Customer Looks
    /// </summary>
    public sealed class CustomerLookSorterWindow : EditorWindow
    {
        const float Thumb = 128f; // AssetPreview 기본 해상도
        static readonly Color LocalColor = new Color(0.35f, 0.75f, 0.45f);
        static readonly Color ForeignColor = new Color(0.95f, 0.6f, 0.2f);

        CustomerRoster roster;
        Vector2 scroll;
        int genderFilter; // 0 전체, 1 남, 2 여

        [MenuItem("Tools/CardShop/Customers/Sort Customer Looks", priority = 62)]
        static void Open() => GetWindow<CustomerLookSorterWindow>("손님 외형 분류");

        void OnEnable() => roster = AssetDatabase.LoadAssetAtPath<CustomerRoster>(CustomerRosterBuilder.RosterPath);

        void OnGUI()
        {
            if (roster == null)
            {
                EditorGUILayout.HelpBox("CustomerRoster가 없습니다.", MessageType.Info);
                if (GUILayout.Button("Build Customer Roster")) roster = CustomerRosterBuilder.Build();
                return;
            }

            int local = 0, foreign = 0;
            foreach (CustomerRoster.Entry e in roster.entries)
                if (e.look == CustomerLook.Local) local++; else foreign++;

            EditorGUILayout.LabelField($"Local {local}명 · Foreign {foreign}명 (등장 가중치 {roster.localWeight} : {roster.foreignWeight})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("썸네일을 클릭하면 Local ↔ Foreign이 바뀝니다. 초록 = Local, 주황 = Foreign", EditorStyles.miniLabel);
            genderFilter = GUILayout.Toolbar(genderFilter, new[] { "전체", "남성", "여성" });

            int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 20f) / (Thumb + 8f)));
            bool loading = false;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            int col = 0;
            EditorGUILayout.BeginHorizontal();
            foreach (CustomerRoster.Entry e in roster.entries)
            {
                if (e.prefab == null) continue;
                if (genderFilter == 1 && e.gender != CustomerGender.Male) continue;
                if (genderFilter == 2 && e.gender != CustomerGender.Female) continue;

                Texture2D preview = AssetPreview.GetAssetPreview(e.prefab);
                if (preview == null && AssetPreview.IsLoadingAssetPreview(e.prefab.GetInstanceID())) loading = true;

                EditorGUILayout.BeginVertical(GUILayout.Width(Thumb));
                Rect r = GUILayoutUtility.GetRect(Thumb, Thumb);
                EditorGUI.DrawRect(r, e.look == CustomerLook.Local ? LocalColor : ForeignColor);
                Rect inner = new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6);
                if (preview != null) GUI.DrawTexture(inner, preview, ScaleMode.ScaleToFit);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    Undo.RecordObject(roster, "손님 외형 분류");
                    e.look = e.look == CustomerLook.Local ? CustomerLook.Foreign : CustomerLook.Local;
                    EditorUtility.SetDirty(roster);
                }
                GUILayout.Label(e.prefab.name.Replace("TCP_", "").Replace("_Character", ""), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                if (++col >= columns)
                {
                    col = 0;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("저장")) AssetDatabase.SaveAssets();
            if (loading) Repaint(); // 썸네일이 비동기로 만들어지는 동안 다시 그림
        }
    }
}
