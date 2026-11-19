using CardShop.Cards;
using UnityEditor;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 손님 외형을 썸네일로 보며 Local(아시아계) / Foreign(외국인)으로, '나이' 보기에서는 어른 / 젊음(목소리 칸)으로 나누는 창.
    /// 외형 판단은 자동으로 할 수 없어 사람이 눈으로 정합니다. 썸네일을 클릭하면 분류가 바뀝니다.
    /// 메뉴: Tools > CardShop > Customers > Sort Customer Looks
    /// </summary>
    public sealed class CustomerLookSorterWindow : EditorWindow
    {
        const float BodyThumb = 128f; // AssetPreview 기본 해상도
        const float FaceThumb = 180f; // 얼굴 보기 — 얼굴로 판단하므로 크게
        static readonly Color LocalColor = new Color(0.35f, 0.75f, 0.45f);
        static readonly Color ForeignColor = new Color(0.95f, 0.6f, 0.2f);

        CustomerRoster roster;
        Vector2 scroll;
        int genderFilter; // 0 전체, 1 남, 2 여
        int mode;         // 0 외형(Local/Foreign), 1 나이(어른/젊음 — 목소리 칸 고르기), 2 성별(목소리 남녀, 11-20 C1)
        static readonly Color AdultColor = new Color(0.45f, 0.45f, 0.5f);
        static readonly Color YoungColor = new Color(0.35f, 0.6f, 0.95f);
        static readonly Color MaleColor = new Color(0.3f, 0.5f, 0.85f);
        static readonly Color FemaleColor = new Color(0.9f, 0.45f, 0.65f);

        [MenuItem("Tools/CardShop/Customers/Sort Customer Looks", priority = 62)]
        static void Open() => GetWindow<CustomerLookSorterWindow>("손님 외형 분류");

        bool faceView = true; // true = 얼굴 확대, false = 전신(AssetPreview)
        CustomerFacePreview faces;

        void OnEnable()
        {
            roster = AssetDatabase.LoadAssetAtPath<CustomerRoster>(CustomerRosterBuilder.RosterPath);
            faces = new CustomerFacePreview();
        }

        void OnDisable()
        {
            faces?.Dispose();
            faces = null;
        }

        void OnGUI()
        {
            if (roster == null)
            {
                EditorGUILayout.HelpBox("CustomerRoster가 없습니다.", MessageType.Info);
                if (GUILayout.Button("Build Customer Roster")) roster = CustomerRosterBuilder.Build();
                return;
            }

            int local = 0, foreign = 0, young = 0;
            foreach (CustomerRoster.Entry e in roster.entries)
            {
                if (e.look == CustomerLook.Local) local++; else foreign++;
                if (e.age == CustomerAge.Young) young++;
            }

            mode = GUILayout.Toolbar(mode, new[] { "외형", "나이 (목소리)", "성별 (목소리)" });
            if (mode == 0)
            {
                EditorGUILayout.LabelField($"Local {local}명 · Foreign {foreign}명 (등장 가중치 {roster.localWeight} : {roster.foreignWeight})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("썸네일을 클릭하면 Local ↔ Foreign이 바뀝니다. 초록 = Local, 주황 = Foreign", EditorStyles.miniLabel);
            }
            else if (mode == 2)
            {
                EditorGUILayout.LabelField("썸네일을 클릭하면 남 ↔ 여가 바뀝니다. 파랑 = 남자 목소리, 분홍 = 여자 목소리. 모델과 다르면 고쳐 주세요", EditorStyles.boldLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"젊음 {young}명 · 어른 {roster.entries.Count - young}명", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("썸네일을 클릭하면 어른 ↔ 젊음이 바뀝니다. 파랑 = 젊음(젊은 목소리 칸), 회색 = 어른", EditorStyles.miniLabel);
            }
            genderFilter = GUILayout.Toolbar(genderFilter, new[] { "전체", "남성", "여성" });
            faceView = GUILayout.Toolbar(faceView ? 0 : 1, new[] { "얼굴 크게", "전신" }) == 0;

            float Thumb = faceView ? FaceThumb : BodyThumb;
            faces.BeginFrame();
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

                Texture preview;
                if (faceView)
                {
                    preview = faces.Get(e.prefab, (int)FaceThumb * 2); // 2배로 찍어 선명하게
                    if (preview == null) loading = true;
                }
                else
                {
                    preview = AssetPreview.GetAssetPreview(e.prefab);
                    if (preview == null && AssetPreview.IsLoadingAssetPreview(e.prefab.GetInstanceID())) loading = true;
                }

                EditorGUILayout.BeginVertical(GUILayout.Width(Thumb));
                Rect r = GUILayoutUtility.GetRect(Thumb, Thumb);
                Color frame = mode == 0 ? (e.look == CustomerLook.Local ? LocalColor : ForeignColor)
                    : mode == 2 ? (e.gender == CustomerGender.Male ? MaleColor : FemaleColor)
                    : (e.age == CustomerAge.Young ? YoungColor : AdultColor);
                EditorGUI.DrawRect(r, frame);
                Rect inner = new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6);
                if (preview != null) GUI.DrawTexture(inner, preview, ScaleMode.ScaleToFit);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    Undo.RecordObject(roster, mode == 0 ? "손님 외형 분류" : mode == 2 ? "손님 성별 분류" : "손님 나이 분류");
                    if (mode == 0) e.look = e.look == CustomerLook.Local ? CustomerLook.Foreign : CustomerLook.Local;
                    else if (mode == 2) e.gender = e.gender == CustomerGender.Male ? CustomerGender.Female : CustomerGender.Male;
                    else e.age = e.age == CustomerAge.Young ? CustomerAge.Adult : CustomerAge.Young;
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
