using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardShop.Cards.EditorTools
{
    /// <summary>
    /// 카드 데이터베이스의 모든 카드를 실제 카드 프리팹으로 찍어 한 장에 모읍니다 (그림 교체 후 확인용).
    /// 메뉴: Tools > CardShop > Render All Cards → Temp/CardGallery.png
    /// 배치 모드: -executeMethod CardShop.Cards.EditorTools.CardGalleryRenderer.RenderFromCommandLine -cardGalleryOut &lt;png&gt;
    /// 빈 씬을 새로 열므로, 열린 씬에 저장 안 한 수정이 있으면 먼저 저장하세요.
    /// </summary>
    public static class CardGalleryRenderer
    {
        const string PrefabPath = "Assets/03. Prefabs/Cards/Card.prefab";
        const int Columns = 10;
        const int TileW = 360, TileH = 500;

        [MenuItem("Tools/CardShop/Render All Cards", priority = 12)]
        static void RenderMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/CardGallery.png");
            Render(path);
            EditorUtility.RevealInFinder(path);
        }

        public static void RenderFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-cardGalleryOut");
            Render(i >= 0 && i + 1 < args.Length ? args[i + 1] : "CardGallery.png");
        }

        static void Render(string outPath)
        {
            // 새 씬을 열면 안 쓰는 에셋이 내려가므로, 카드 데이터는 씬을 연 뒤에 읽습니다.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var database = AssetDatabase.LoadAssetAtPath<CardDatabase>(DataPaths.CardDatabase);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(30f, 20f, 0f);

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.13f, 0.2f);
            cam.orthographic = true;
            cam.orthographicSize = CardView.Height * 0.5f + 0.003f;
            cam.transform.position = new Vector3(0f, 0f, -1f);

            // 카드 한 장을 다시 쓰며 한 장씩 찍습니다
            var card = Object.Instantiate(prefab);
            if (card.TryGetComponent(out Rigidbody body)) body.isKinematic = true;
            var view = card.GetComponent<CardView>();
            var rt = new RenderTexture(TileW, TileH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;

            int count = database.cards.Count;
            int rows = (count + Columns - 1) / Columns;
            var sheet = new Texture2D(Columns * TileW, rows * TileH, TextureFormat.RGBA32, false);
            var clear = new Color32[sheet.width * sheet.height];
            for (int p = 0; p < clear.Length; p++) clear[p] = new Color32(40, 33, 51, 255);
            sheet.SetPixels32(clear);

            for (int n = 0; n < count; n++)
            {
                view.Bind(database.cards[n]);
                foreach (var t in card.GetComponentsInChildren<TMP_Text>()) t.ForceMeshUpdate();
                cam.Render();
                RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(0, 0, TileW, TileH), n % Columns * TileW, (rows - 1 - n / Columns) * TileH);
            }
            sheet.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            File.WriteAllBytes(outPath, sheet.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(sheet);
            rt.Release();
            Debug.Log($"[CardGalleryRenderer] 카드 {count}장 → {outPath}");
        }
    }
}
