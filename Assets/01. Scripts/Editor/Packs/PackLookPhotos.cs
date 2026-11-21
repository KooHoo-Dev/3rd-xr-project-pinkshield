using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// MALLANG 팩 · 상자 겉모습 확인 사진 (11-21 V1): 카드팩 앞 · 뒤, 손님 팩, 손님 상자, 부스터 박스를 빈 장면에 놓고 한 장으로.
    /// 결과: DevLog/screenshots/V1/pack-look-photos.png
    /// </summary>
    public static class PackLookPhotos
    {
        const string OutPath = "DevLog/screenshots/V1/pack-look-photos.png";
        const int Tile = 320;

        static readonly (string path, float yaw, float dist)[] Shots =
        {
            ("Assets/03. Prefabs/Packs/Pack.prefab", 0f, 0.32f),
            ("Assets/03. Prefabs/Packs/Pack.prefab", 180f, 0.32f),
            ("Assets/03. Prefabs/Customers/CustomerItem_Pack.prefab", 0f, 0.32f),
            ("Assets/03. Prefabs/Customers/CustomerItem_Box.prefab", 20f, 0.6f),
            ("Assets/03. Prefabs/Interior/Decor/BoosterBox_Pink.prefab", 20f, 0.6f),
        };

        [MenuItem("Tools/CardShop/Build/Pack Look Photos", priority = 22)]
        public static void Photograph()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35f, 160f, 0f);
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.65f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.25f, 0.27f, 0.32f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.01f;
            var rt = new RenderTexture(Tile, Tile, 24);
            cam.targetTexture = rt;
            var sheet = new Texture2D(Tile * Shots.Length, Tile, TextureFormat.RGB24, false);

            for (int i = 0; i < Shots.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Shots[i].path);
                if (prefab == null) continue;
                GameObject go = Object.Instantiate(prefab, Vector3.zero, Quaternion.Euler(0f, Shots[i].yaw, 0f));
                Bounds b = new Bounds(go.transform.position, Vector3.zero);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                cam.transform.position = b.center + new Vector3(0f, 0.15f, -1f).normalized * Shots[i].dist;
                cam.transform.LookAt(b.center);
                cam.Render();
                RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(0, 0, Tile, Tile), i * Tile, 0);
                Object.DestroyImmediate(go);
            }
            RenderTexture.active = null;
            sheet.Apply();
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(rt);
            Debug.Log($"[PackLookPhotos] {OutPath}");
        }

        public static void PhotographFromCommandLine()
        {
            Photograph();
            EditorApplication.Exit(0);
        }
    }
}
