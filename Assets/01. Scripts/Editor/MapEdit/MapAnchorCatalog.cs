using System;
using System.Collections.Generic;
using CardShop.Core;
using CardShop.Interaction;
using CardShop.Presentation;
using CardShop.Tutorial;
using CardShop.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 맵 수정 도구가 아는 자리표 목록 (맵_수정_도구_설계 3장). 실행 중에 생기고 코드 숫자로 자리가 정해지던 물건들입니다.
    /// 자리표를 만들면 지금 코드 기본 자리에 생기므로, 만든 직후 게임 모습은 그대로이고 옮긴 만큼만 달라집니다.
    /// 새 물건을 옮기게 하려면: 그 물건 코드가 <see cref="PlacementAnchor.Find"/>로 자리를 읽게 하고 여기 한 줄을 더합니다.
    /// </summary>
    public static class MapAnchorCatalog
    {
        public const string GroupName = "PlacementAnchors";

        public sealed class Entry
        {
            public string key;
            public string label;
            /// <summary>코드 기본 자리. 기준 물건(책상 · 집 실내)이 씬에 없으면 null</summary>
            public Func<Pose?> defaultPose;
            public Vector3 previewCenter, previewSize;
        }

        public static readonly IReadOnlyList<Entry> All = new[]
        {
            new Entry { key = TutorialSigns.ControlsKey, label = "조작 안내 판 (타이틀 · 인트로)",
                defaultPose = () => TutorialSigns.ControlsDefault, previewSize = Board(TutorialSigns.ControlsSize) },
            new Entry { key = TutorialSigns.RulesKey, label = "알바 수칙 포스터",
                defaultPose = () => TutorialSigns.RulesDefault, previewSize = Board(TutorialSigns.RulesSize) },
            new Entry { key = ShopTimerBoard.AnchorKey, label = "가게 디지털 시계 (영업 중)",
                defaultPose = () => ShopTimerBoard.DefaultPose, previewSize = Board(ShopTimerBoard.BoardSize) },
            new Entry { key = ResignLetter.AnchorKey, label = "사직서 봉투 (무한 모드 · 집 책상)",
                defaultPose = ResignLetter.DefaultPose, previewCenter = new Vector3(0f, 0.02f, 0f), previewSize = new Vector3(0.24f, 0.04f, 0.14f) },
            new Entry { key = RoomUpgradeView.BoxAnchorKey, label = "택배 상자 줄 (집, 첫 상자 = 왼쪽 끝)",
                defaultPose = () => HomeRoom.Interior() is Transform t ? RoomUpgradeView.DefaultBoxPose(t) : (Pose?)null,
                // 한 줄 4개(0.45m 간격) + 상자 0.4m. 5개 넘게 오면 0.5m 뒤에 둘째 줄
                previewCenter = new Vector3((RoomUpgradeView.BoxesPerRow - 1) * RoomUpgradeView.BoxStepX / 2f, 0.15f, 0f),
                previewSize = new Vector3((RoomUpgradeView.BoxesPerRow - 1) * RoomUpgradeView.BoxStepX + 0.4f, 0.3f, 0.4f) },
        };

        static Vector3 Board(Vector2 size) => new Vector3(size.x, size.y, 0.02f);

        /// <summary>자리표를 코드 기본 자리에 만들고 선택합니다. 이미 있으면 그것을 선택. 기본 자리를 모르면 null</summary>
        public static PlacementAnchor Create(Entry e)
        {
            PlacementAnchor existing = PlacementAnchor.Get(e.key);
            if (existing != null) { Selection.activeGameObject = existing.gameObject; return existing; }
            Pose? pose = e.defaultPose();
            if (pose == null)
            {
                Debug.LogWarning($"[MapEdit] '{e.label}'의 기본 자리를 찾지 못했습니다 (집 실내 · 책상이 씬에 있는지 확인).");
                return null;
            }

            var go = new GameObject("Anchor_" + e.key.Replace('/', '_'));
            Undo.RegisterCreatedObjectUndo(go, "Create Placement Anchor");
            go.transform.SetParent(Group(), true);
            go.transform.SetPositionAndRotation(pose.Value.position, pose.Value.rotation);
            var a = go.AddComponent<PlacementAnchor>();
            a.key = e.key;
            a.previewCenter = e.previewCenter;
            a.previewSize = e.previewSize;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
            return a;
        }

        /// <summary>자리표를 코드 기본 자리로 되돌립니다 (Ctrl+Z 가능).</summary>
        public static void ResetToDefault(PlacementAnchor a)
        {
            Entry e = Find(a.key);
            Pose? pose = e?.defaultPose();
            if (pose == null) return;
            Undo.RecordObject(a.transform, "Reset Placement Anchor");
            a.transform.SetPositionAndRotation(pose.Value.position, pose.Value.rotation);
        }

        public static Entry Find(string key)
        {
            foreach (Entry e in All) if (e.key == key) return e;
            return null;
        }

        /// <summary>자리표를 모아 두는 씬 맨 위 빈 오브젝트</summary>
        static Transform Group()
        {
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == GroupName) return root.transform;
            var g = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(g, "Create Placement Anchor");
            return g.transform;
        }
    }
}
