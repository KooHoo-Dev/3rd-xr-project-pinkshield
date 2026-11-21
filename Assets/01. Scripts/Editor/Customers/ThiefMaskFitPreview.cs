using System.IO;
using CardShop.Cards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardShop.EditorTools.Customers
{
    /// <summary>
    /// 복면 자리 확인 사진 (11-21 V2): 로스터 모델마다 복면을 ThiefBody.WearMask와 같은 규칙으로 씌우고
    /// 앞 · 옆 얼굴을 찍어 한 장(DevLog/screenshots/V2/thief-mask-fit.png)으로 모읍니다. 이상한 칸 번호 = 로스터 순서.
    /// 빈 장면에서 찍으므로 열려 있는 씬을 저장하지 않았으면 먼저 저장하라고 묻습니다.
    /// </summary>
    public static class ThiefMaskFitPreview
    {
        const string RosterPath = "Assets/02. Data/Configs/CustomerRoster.asset";
        const string MaskPath = "Assets/03. Prefabs/Customers/ThiefMask.prefab";
        const string OutPath = "DevLog/screenshots/V2/thief-mask-fit.png";
        const int Tile = 160, Cols = 12;

        [MenuItem("Tools/CardShop/Customers/Thief Mask Fit Photos", priority = 65)]
        public static void Photograph()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var roster = AssetDatabase.LoadAssetAtPath<CustomerRoster>(RosterPath);
            var mask = AssetDatabase.LoadAssetAtPath<GameObject>(MaskPath);
            if (roster == null || mask == null) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            lightGo.transform.rotation = Quaternion.Euler(30f, 200f, 0f);
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.25f, 0.27f, 0.32f);
            cam.fieldOfView = 22f;
            var rt = new RenderTexture(Tile, Tile, 24);
            cam.targetTexture = rt;

            int n = roster.entries.Count;
            int rows = Mathf.CeilToInt(n * 2f / Cols);
            var sheet = new Texture2D(Cols * Tile, rows * Tile, TextureFormat.RGB24, false);
            for (int i = 0; i < n; i++)
            {
                CustomerRoster.Entry e = roster.entries[i];
                if (e?.prefab == null) continue;
                GameObject model = Object.Instantiate(e.prefab, Vector3.zero, Quaternion.identity);
                var animator = model.GetComponentInChildren<Animator>();
                Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                if (head != null)
                {
                    GameObject m = Object.Instantiate(mask);
                    bool fitted = e.maskScale > 0f;
                    m.transform.localScale = mask.transform.localScale * (fitted ? e.maskScale : 1f);
                    Vector3 center = head.position + (fitted ? e.maskOffset : Vector3.up * 0.09f);
                    m.transform.SetPositionAndRotation(center, Quaternion.LookRotation(Vector3.forward, Vector3.up));
                    m.transform.SetParent(model.transform, true);
                    m.SetActive(true);
                    Vector3 aim = head.position + Vector3.up * 0.08f;
                    for (int side = 0; side < 2; side++)
                    {
                        Vector3 dir = side == 0 ? Vector3.forward : Vector3.right;
                        cam.transform.position = aim + dir * 1.3f;
                        cam.transform.LookAt(aim);
                        cam.Render();
                        RenderTexture.active = rt;
                        int slot = i * 2 + side;
                        sheet.ReadPixels(new Rect(0, 0, Tile, Tile), (slot % Cols) * Tile, (rows - 1 - slot / Cols) * Tile);
                    }
                }
                Object.DestroyImmediate(model);
            }
            RenderTexture.active = null;
            sheet.Apply();
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(rt);
            Debug.Log($"[ThiefMaskFit] 사진 {n}명 → {OutPath} (한 줄 {Cols / 2}명, 칸마다 앞 · 옆)");
        }

        public static void PhotographFromCommandLine()
        {
            Photograph();
            EditorApplication.Exit(0);
        }
    }
}
