using CardShop.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>열린 씬에 사장님 메모 자리(TutorialMemoAnchor)를 기본 자리에 만들고 선택합니다 (11-20 J3). 이미 있으면 그것을 선택.</summary>
    public static class TutorialMemoAnchorMenu
    {
        [MenuItem("Tools/CardShop/Tutorial/Create Memo Anchor")]
        static void Create()
        {
            var existing = Object.FindAnyObjectByType<TutorialMemoAnchor>();
            if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
            Pose p = TutorialMemoAnchor.DefaultPose;
            var go = new GameObject("TutorialMemoAnchor");
            go.transform.SetPositionAndRotation(p.position, p.rotation);
            go.AddComponent<TutorialMemoAnchor>();
            Undo.RegisterCreatedObjectUndo(go, "Create Memo Anchor");
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
        }
    }
}
