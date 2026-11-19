using System.Collections.Generic;
using CardShop.Core;
using CardShop.Presentation;
using CardShop.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 맵 수정 모드 (맵_수정_도구_설계 1 · 4장): 게임을 실행하지 않은 채, 실행 중에만 보이는 것을 씬에 꺼내 보여 줍니다.
    /// ① 씬에서 꺼져 있는 오브젝트를 켭니다 (씬 맨 위 묶음별로 고를 수 있음).
    /// ② 자리표마다 실제 물건 크기의 반투명 모형을 띄웁니다. 모형은 씬 파일에 저장되지 않습니다(HideFlags.DontSave).
    ///
    /// 켠 것을 그대로 저장하면 게임 첫 상태가 망가지므로(예: 일시정지 메뉴가 처음부터 뜸) 다음 때 원래대로 끕니다:
    /// 끝내기 · Play 시작 · 씬 저장 직전(저장 뒤 다시 켬) · 씬 닫기 · 스크립트 다시 불러오기.
    /// Play를 끝내거나 스크립트를 다시 불러온 뒤에는 모드를 이어서 다시 켭니다.
    /// </summary>
    [InitializeOnLoad]
    public static class MapEditMode
    {
        const string ResumeKey = "CardShop.MapEdit.Resume";
        const string OffGroupsKey = "CardShop.MapEdit.OffGroups";
        const HideFlags PreviewFlags = HideFlags.DontSave | HideFlags.NotEditable; // 계층 창에는 보임 (숨기면 씬 뷰에서 클릭이 안 될 수 있음)

        struct TurnedOn
        {
            public GameObject go;
            public bool hadActiveOverride; // 프리팹 안 오브젝트: 켜기 전에 "켜짐" 오버라이드가 없었으면 끌 때 오버라이드도 지움
        }

        static readonly List<TurnedOn> turnedOn = new List<TurnedOn>();
        static readonly List<GameObject> previews = new List<GameObject>();
        static Material previewMaterial;

        public static bool Active { get; private set; }
        public static event System.Action Changed;

        static MapEditMode()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorSceneManager.sceneSaving += (scene, path) => { if (Active) Hide(); };
            EditorSceneManager.sceneSaved += scene => { if (Active) Show(); };
            EditorSceneManager.sceneClosing += (scene, removing) => { if (Active) Stop(); };
            AssemblyReloadEvents.beforeAssemblyReload += () => { if (Active) { Hide(); SessionState.SetBool(ResumeKey, true); } };
            if (SessionState.GetBool(ResumeKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += Resume;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode && Active) { Hide(); Active = false; SessionState.SetBool(ResumeKey, true); }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ResumeKey, false)) Resume();
        }

        // Play를 끝낼 때 스크립트 다시 불러오기와 EnteredEditMode가 둘 다 부를 수 있어 한 번만 이어 켬
        static void Resume()
        {
            if (!SessionState.GetBool(ResumeKey, false)) return;
            SessionState.SetBool(ResumeKey, false);
            Start();
        }

        public static void Start()
        {
            if (Active || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Active = true;
            Show();
        }

        public static void Stop()
        {
            Hide();
            Active = false;
            SessionState.SetBool(ResumeKey, false);
            Changed?.Invoke();
        }

        /// <summary>묶음 고르기를 바꾼 뒤 다시 그립니다.</summary>
        public static void Refresh()
        {
            if (!Active) return;
            Hide();
            Show();
        }

        // ------------------------------------------------------------------ 묶음 (씬 맨 위 오브젝트) 고르기

        public static bool IsGroupOn(string root) => !OffGroups().Contains(root);

        public static void SetGroupOn(string root, bool on)
        {
            HashSet<string> off = OffGroups();
            if (on) off.Remove(root); else off.Add(root);
            SessionState.SetString(OffGroupsKey, string.Join("\n", off));
            Refresh();
        }

        static HashSet<string> OffGroups() =>
            new HashSet<string>(SessionState.GetString(OffGroupsKey, "").Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries));

        /// <summary>씬 맨 위 오브젝트 중 꺼진 자식이 있는 것과 그 수 (창에 보여 줄 목록)</summary>
        public static List<KeyValuePair<string, int>> HiddenGroups()
        {
            var list = new List<KeyValuePair<string, int>>();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if ((root.hideFlags & HideFlags.DontSave) != 0) continue;
                int n = 0;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (!t.gameObject.activeSelf || WasTurnedOn(t.gameObject)) n++;
                if (n > 0) list.Add(new KeyValuePair<string, int>(root.name, n));
            }
            return list;
        }

        static bool WasTurnedOn(GameObject go)
        {
            foreach (TurnedOn t in turnedOn) if (t.go == go) return true;
            return false;
        }

        // ------------------------------------------------------------------ 켜기 · 끄기

        static void Show()
        {
            HashSet<string> off = OffGroups();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if ((root.hideFlags & HideFlags.DontSave) != 0 || off.Contains(root.name)) continue;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject go = t.gameObject;
                    if (go.activeSelf || (go.hideFlags & HideFlags.DontSave) != 0) continue;
                    turnedOn.Add(new TurnedOn { go = go, hadActiveOverride = HasActiveOverride(go) });
                    go.SetActive(true);
                }
            }
            CreatePreviews();
            SceneView.RepaintAll();
            Changed?.Invoke();
        }

        static void Hide()
        {
            // 켠 순서(부모 → 자식)의 반대로 끔
            for (int i = turnedOn.Count - 1; i >= 0; i--)
            {
                TurnedOn t = turnedOn[i];
                if (t.go == null) continue;
                t.go.SetActive(false);
                if (!t.hadActiveOverride && PrefabUtility.IsPartOfPrefabInstance(t.go))
                {
                    var so = new SerializedObject(t.go);
                    SerializedProperty p = so.FindProperty("m_IsActive");
                    if (p != null && p.prefabOverride) PrefabUtility.RevertPropertyOverride(p, InteractionMode.AutomatedAction);
                }
            }
            turnedOn.Clear();
            foreach (GameObject p in previews) if (p != null) Object.DestroyImmediate(p);
            previews.Clear();
            SceneView.RepaintAll();
        }

        static bool HasActiveOverride(GameObject go)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(go)) return false;
            SerializedProperty p = new SerializedObject(go).FindProperty("m_IsActive");
            return p != null && p.prefabOverride;
        }

        // ------------------------------------------------------------------ 자리표 미리보기 모형

        static void CreatePreviews()
        {
            foreach (PlacementAnchor a in Object.FindObjectsByType<PlacementAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                AddPreview(a.transform, a.previewCenter, a.previewSize, new Color(0.3f, 0.85f, 1f, 0.45f));
            foreach (CardCaseAnchor a in Object.FindObjectsByType<CardCaseAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                AddPreview(a.transform, Vector3.zero, new Vector3(0.56f, 0.38f, 0.08f), new Color(0.9f, 0.7f, 0.4f, 0.45f));
            foreach (TutorialMemoAnchor a in Object.FindObjectsByType<TutorialMemoAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                AddPreview(a.transform, Vector3.zero, new Vector3(0.2f, 0.14f, 0.01f), new Color(1f, 0.85f, 0.4f, 0.45f));
        }

        static void AddPreview(Transform anchor, Vector3 center, Vector3 size, Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.name = "MapEditPreview";
            cube.transform.SetParent(anchor, false);
            cube.transform.localPosition = center;
            cube.transform.localScale = size;
            var r = cube.GetComponent<MeshRenderer>();
            r.sharedMaterial = PreviewMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            r.SetPropertyBlock(block);
            cube.hideFlags = PreviewFlags;
            previews.Add(cube);
        }

        /// <summary>URP Unlit 반투명 재질 (한 번 만들어 같이 씀, 저장 안 함)</summary>
        static Material PreviewMaterial()
        {
            if (previewMaterial != null) return previewMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            previewMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, name = "MapEditPreview" };
            previewMaterial.SetFloat("_Surface", 1f); // Transparent
            previewMaterial.SetFloat("_Blend", 0f);   // Alpha
            previewMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            previewMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            previewMaterial.SetFloat("_ZWrite", 0f);
            previewMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            previewMaterial.renderQueue = (int)RenderQueue.Transparent;
            return previewMaterial;
        }
    }
}
