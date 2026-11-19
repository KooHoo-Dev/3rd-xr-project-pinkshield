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

namespace CardShop.EditorTools
{
    /// <summary>열린 씬에 카드 보관함 자리(CardCaseAnchor)를 만들고 선택합니다 (11-20 J5 · J6). 기본 자리: 집 모니터 위 근처 — 씬에서 옮기세요.</summary>
    public static class CardCaseAnchorMenu
    {
        [UnityEditor.MenuItem("Tools/CardShop/Home/Create Card Case Anchor")]
        static void Create()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<CardShop.Presentation.CardCaseAnchor>();
            if (existing != null) { UnityEditor.Selection.activeGameObject = existing.gameObject; return; }
            var screen = UnityEngine.Object.FindAnyObjectByType<CardShop.UI.CommunityScreen>(UnityEngine.FindObjectsInactive.Include);
            var go = new UnityEngine.GameObject("CardCaseAnchor");
            if (screen != null)
                go.transform.SetPositionAndRotation(screen.transform.position + UnityEngine.Vector3.up * 0.68f + screen.transform.forward * 0.08f,
                    UnityEngine.Quaternion.LookRotation(screen.transform.rotation * UnityEngine.Vector3.back, UnityEngine.Vector3.up));
            go.AddComponent<CardShop.Presentation.CardCaseAnchor>();
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Card Case Anchor");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            UnityEditor.Selection.activeGameObject = go;
        }
    }
}
