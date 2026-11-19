using System.Collections.Generic;
using CardShop.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 맵 수정 도구 창 (맵_수정_도구_설계). 메뉴: Tools > CardShop > Map Edit
    /// 시작하면 실행 중에만 보이는 것을 씬에 꺼내고(<see cref="MapEditMode"/>), 씬 뷰에서 평소처럼 옮긴 뒤 씬을 저장하면 됩니다.
    /// 이 창은 버튼과 목록만 보여 주고, 켜기 · 끄기 · 자리표 만들기는 각 클래스가 맡습니다.
    /// </summary>
    public sealed class MapEditWindow : EditorWindow
    {
        Vector2 scroll;
        bool showGroups = true, showAnchors = true, showMoved = true;

        [MenuItem("Tools/CardShop/Map Edit", priority = 31)]
        static void Open() => GetWindow<MapEditWindow>("맵 수정");

        void OnEnable()
        {
            MapEditMode.Changed += Repaint;
            Selection.selectionChanged += Repaint;
        }

        void OnDisable()
        {
            MapEditMode.Changed -= Repaint;
            Selection.selectionChanged -= Repaint;
        }

        void OnGUI()
        {
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 중에는 쓸 수 없습니다. Play 중에 옮긴 자리는 Play를 끄면 사라집니다.\nPlay를 끄면 맵 수정 모드가 다시 켜집니다.", MessageType.Info);
                return;
            }

            DrawStartStop();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSelection();
            if (MapEditMode.Active) DrawGroups();
            DrawAnchors();
            DrawMoved();
            EditorGUILayout.EndScrollView();
        }

        void DrawStartStop()
        {
            EditorGUILayout.Space(4);
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = MapEditMode.Active ? new Color(1f, 0.6f, 0.6f) : new Color(0.6f, 1f, 0.7f);
            if (GUILayout.Button(MapEditMode.Active ? "맵 수정 끝내기 (켠 것 원래대로)" : "맵 수정 시작 (숨은 것 · 실행 중 물건 보이기)", GUILayout.Height(32)))
            {
                if (MapEditMode.Active) MapEditMode.Stop(); else MapEditMode.Start();
            }
            GUI.backgroundColor = old;

            if (MapEditMode.Active)
            {
                EditorGUILayout.HelpBox("씬 뷰에서 옮기고 Ctrl+S로 저장하세요. 저장할 때는 도구가 켠 것을 잠깐 끄고 저장하므로 게임 첫 상태는 그대로입니다.\n" +
                                        "파란 반투명 상자 = 실행 중에 생기는 물건의 자리표(클릭하면 자리표가 선택됨). 모형은 저장되지 않습니다.", MessageType.None);
                RouteGizmoDrawer.Show = EditorGUILayout.ToggleLeft("손님 동선 지점 보기 (원 = 손님 한 명 자리)", RouteGizmoDrawer.Show);
            }
        }

        // ------------------------------------------------------------------ 선택한 오브젝트

        void DrawSelection()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null || !go.scene.IsValid()) return;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("선택: " + go.name, EditorStyles.boldLabel);

            var anchor = go.GetComponent<PlacementAnchor>();
            if (anchor != null)
            {
                MapAnchorCatalog.Entry e = MapAnchorCatalog.Find(anchor.key);
                EditorGUILayout.LabelField("자리표", e != null ? e.label : anchor.key);
                if (e != null && GUILayout.Button("코드 기본 자리로")) MapAnchorCatalog.ResetToDefault(anchor);
                return;
            }

            var mark = go.GetComponent<PlacementKey>();
            if (mark != null)
            {
                EditorGUILayout.LabelField("이름표", mark.key + (mark.addedByHand ? " (손으로 붙임)" : ""));
                EditorGUILayout.LabelField("상태", mark.IsMoved ? "기본 자리에서 옮김 — Build Shop Scene 뒤에도 유지" : "기본 자리");
                using (new EditorGUI.DisabledScope(!mark.IsMoved))
                    if (GUILayout.Button("기본 자리로")) ResetToDefault(mark);
                return;
            }

            EditorGUILayout.HelpBox("이름표(PlacementKey)가 없습니다. 씬을 저장하면 옮긴 자리는 남지만, Build Shop Scene을 다시 실행하면 사라질 수 있습니다.\n" +
                                    "빌더가 다시 만드는 물건이면 이름표를 붙여 두세요 (키 = 계층 경로: " + HierarchyPath.Of(go.transform) + ")", MessageType.Warning);
            if (GUILayout.Button("이름표 붙이기"))
            {
                Undo.AddComponent<PlacementKey>(go);
                PlacementKey.MarkByHand(go);
                EditorSceneManager.MarkSceneDirty(go.scene);
            }
        }

        static void ResetToDefault(PlacementKey mark)
        {
            Undo.RecordObject(mark.transform, "Reset Placement");
            mark.transform.localPosition = mark.defaultLocalPosition;
            mark.transform.localRotation = mark.defaultLocalRotation;
            mark.transform.localScale = mark.defaultLocalScale;
        }

        // ------------------------------------------------------------------ 숨은 것 묶음

        void DrawGroups()
        {
            List<KeyValuePair<string, int>> groups = MapEditMode.HiddenGroups();
            EditorGUILayout.Space(6);
            showGroups = EditorGUILayout.Foldout(showGroups, $"숨은 것 보이기 (묶음 {groups.Count}개)", true);
            if (!showGroups) return;
            EditorGUILayout.LabelField("겹쳐서 고르기 어려우면 묶음을 꺼 두세요.", EditorStyles.miniLabel);
            foreach (KeyValuePair<string, int> g in groups)
            {
                bool on = MapEditMode.IsGroupOn(g.Key);
                bool next = EditorGUILayout.ToggleLeft($"{g.Key}  ({g.Value}개)", on);
                if (next != on) MapEditMode.SetGroupOn(g.Key, next);
            }
        }

        // ------------------------------------------------------------------ 자리표

        void DrawAnchors()
        {
            EditorGUILayout.Space(6);
            showAnchors = EditorGUILayout.Foldout(showAnchors, "실행 중에 생기는 물건 자리표", true);
            if (!showAnchors) return;
            EditorGUILayout.LabelField("자리표를 만들면 지금 기본 자리에 생깁니다. 지우면 다시 기본 자리로 돌아갑니다.", EditorStyles.miniLabel);

            foreach (MapAnchorCatalog.Entry e in MapAnchorCatalog.All)
            {
                PlacementAnchor a = PlacementAnchor.Get(e.key);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(e.label, a != null ? EditorStyles.boldLabel : EditorStyles.label);
                    if (GUILayout.Button(a != null ? "선택" : "만들기", GUILayout.Width(60)))
                    {
                        if (a != null) Select(a.gameObject);
                        else if (MapAnchorCatalog.Create(e) != null) MapEditMode.Refresh();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("카드 보관함 자리표")) { EditorApplication.ExecuteMenuItem("Tools/CardShop/Home/Create Card Case Anchor"); MapEditMode.Refresh(); }
                if (GUILayout.Button("사장님 메모 자리표")) { EditorApplication.ExecuteMenuItem("Tools/CardShop/Tutorial/Create Memo Anchor"); MapEditMode.Refresh(); }
            }
            if (MapEditMode.Active && GUILayout.Button("모형 다시 그리기")) MapEditMode.Refresh();
        }

        // ------------------------------------------------------------------ 옮긴 것

        void DrawMoved()
        {
            var moved = new List<PlacementKey>();
            foreach (PlacementKey k in FindObjectsByType<PlacementKey>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (k.IsMoved) moved.Add(k);
            EditorGUILayout.Space(6);
            showMoved = EditorGUILayout.Foldout(showMoved, $"기본 자리에서 옮긴 것 ({moved.Count}개)", true);
            if (!showMoved) return;
            foreach (PlacementKey k in moved)
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(k.key);
                    if (GUILayout.Button("선택", GUILayout.Width(44))) Select(k.gameObject);
                    if (GUILayout.Button("기본 자리로", GUILayout.Width(76))) ResetToDefault(k);
                }
        }

        static void Select(GameObject go)
        {
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        void OnInspectorUpdate() => Repaint(); // 씬 뷰에서 옮기는 동안 "옮김" 상태를 갱신
    }
}
