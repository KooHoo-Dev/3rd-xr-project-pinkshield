using System.Collections.Generic;
using CardShop.Customers;
using UnityEditor;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 맵 수정 모드에서 손님 동선 지점을 씬 뷰에 그립니다 (맵_수정_도구_설계 6장).
    /// 진열대 · 계산대 같은 가구를 옮겼을 때 손님이 서는 자리나 지나는 길과 겹치는지 보려는 것입니다.
    /// 지점은 CustomerDirector에 연결된 씬 Transform이라 직접 옮길 수 있습니다(PlacementKey로 Build Shop Scene 뒤에도 유지).
    /// 원 = 손님 한 명이 서는 넓이(지름 0.5m). 선은 지점을 지나는 차례를 이은 것이며, 손님은 지점 사이를 직선으로 걷습니다.
    /// </summary>
    [InitializeOnLoad]
    public static class RouteGizmoDrawer
    {
        const string PrefKey = "CardShop.MapEdit.ShowRoutes";
        const float Radius = 0.25f;

        static readonly Color Entry = new Color(1f, 0.6f, 0.2f);
        static readonly Color Queue = new Color(1f, 0.9f, 0.2f);
        static readonly Color Browse = new Color(0.4f, 1f, 0.5f);
        static readonly Color Exit = new Color(1f, 0.4f, 0.4f);

        public static bool Show
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set { EditorPrefs.SetBool(PrefKey, value); SceneView.RepaintAll(); }
        }

        static RouteGizmoDrawer() => SceneView.duringSceneGui += Draw;

        static void Draw(SceneView view)
        {
            if (!MapEditMode.Active || !Show || Event.current.type != EventType.Repaint) return;
            var director = Object.FindAnyObjectByType<CustomerDirector>(FindObjectsInactive.Include);
            if (director == null) return;
            var so = new SerializedObject(director);

            Transform outside = Ref(so, "outside"), doorOutside = Ref(so, "doorOutside"), door = Ref(so, "door");
            Transform entryTurn = Ref(so, "entryTurn"), aisleLow = Ref(so, "aisleLow"), counterExit = Ref(so, "counterExit");
            List<Transform> queue = Refs(so, "queueSlots"), browse = Refs(so, "browseSpots"), wait = Refs(so, "browseWaitSlots");

            // 들어오기: 밖 → 문 밖 → 문 → 계산대 줄 맨 뒤 (줄 · 구경 자리로 갈라짐)
            Path(Entry, outside, doorOutside, door);
            if (queue.Count > 0) Path(Entry, door, queue[queue.Count - 1]);
            if (wait.Count > 0) Path(Entry, door, wait[wait.Count - 1]);
            Path(new Color(0.75f, 0.5f, 1f), door, entryTurn, aisleLow); // 카드 도둑 길

            // 계산대 줄 (0번 = 결제 자리) · 구경 대기 줄
            Line(Queue, queue, "줄 ");
            Line(Browse, wait, "구경 대기 ");
            for (int i = 0; i < browse.Count; i++) Spot(Browse, browse[i], "구경 " + i);

            // 나가기: 결제 자리 → 카운터 오른쪽 → 문
            if (queue.Count > 0) Path(Exit, queue[0], counterExit, door);
            Spot(Entry, door, "문");
            Spot(Exit, counterExit, "나가는 곳");
        }

        static void Line(Color c, List<Transform> slots, string label)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                Spot(c, slots[i], label + i);
                if (i > 0) Path(c, slots[i - 1], slots[i]);
            }
        }

        static void Spot(Color c, Transform t, string label)
        {
            if (t == null) return;
            Handles.color = c;
            Handles.DrawWireDisc(t.position + Vector3.up * 0.02f, Vector3.up, Radius, 2f);
            Handles.DrawLine(t.position + Vector3.up * 0.02f, t.position + Vector3.up * 0.02f + t.forward * Radius, 2f); // 바라보는 쪽
            Handles.Label(t.position + Vector3.up * 0.1f, label, EditorStyles.whiteMiniLabel);
        }

        static void Path(Color c, params Transform[] points)
        {
            Handles.color = c;
            for (int i = 1; i < points.Length; i++)
                if (points[i - 1] != null && points[i] != null)
                    Handles.DrawDottedLine(points[i - 1].position + Vector3.up * 0.02f, points[i].position + Vector3.up * 0.02f, 4f);
        }

        static Transform Ref(SerializedObject so, string field) => so.FindProperty(field)?.objectReferenceValue as Transform;

        static List<Transform> Refs(SerializedObject so, string field)
        {
            var list = new List<Transform>();
            SerializedProperty p = so.FindProperty(field);
            if (p == null || !p.isArray) return list;
            for (int i = 0; i < p.arraySize; i++)
                if (p.GetArrayElementAtIndex(i).objectReferenceValue is Transform t) list.Add(t);
            return list;
        }
    }
}
