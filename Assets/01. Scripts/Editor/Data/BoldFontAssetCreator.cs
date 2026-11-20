using System.IO;
using CardShop.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CardShop.EditorTools
{
    /// <summary>
    /// Noto Sans KR Bold 글꼴 파일(.otf/.ttf)로 TMP 글꼴 에셋을 만들어 Resources/Fonts에 둡니다 (11-20 결정: 글꼴 하나 · 모두 굵게).
    /// 지금 Regular 에셋과 같은 "동적(Dynamic)" 방식이라 쓰는 글자만 그때그때 아틀라스에 그립니다 (멀티 아틀라스 켬).
    /// 에셋이 생기면 UiFonts가 실행할 때 자동으로 그것을 씁니다 (없으면 Regular + 굵게 표시).
    /// </summary>
    internal static class BoldFontAssetCreator
    {
        const string SourceFolder = "Assets/99. External Assets/Font/Noto_Sans_KR";
        const string OutputFolder = "Assets/Resources/Fonts";

        [MenuItem("CardShop/UI/Bold 글꼴 에셋 만들기")]
        static void Create()
        {
            Font font = FindSource();
            if (font == null)
            {
                EditorUtility.DisplayDialog("Bold 글꼴 에셋",
                    $"{SourceFolder} 에 NotoSansKR-Bold.otf (또는 .ttf)를 넣어 주세요.\nGoogle Fonts의 Noto Sans KR (OFL 무료 글꼴)에 들어 있습니다.", "확인");
                return;
            }

            string path = $"{OutputFolder}/{Path.GetFileName(UiFonts.BoldResource)}.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null &&
                !EditorUtility.DisplayDialog("Bold 글꼴 에셋", $"{path} 가 이미 있습니다. 다시 만들까요?", "다시 만들기", "취소"))
                return;

            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.DeleteAsset(path);

            // Regular 에셋과 같은 설정: 샘플 90 · 여백 9 · SDFAA · 1024 아틀라스 · 동적 · 멀티 아틀라스
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            asset.name = Path.GetFileName(UiFonts.BoldResource);
            AssetDatabase.CreateAsset(asset, path);
            // 아틀라스 · 머티리얼을 에셋 안에 같이 저장 (안 하면 씬을 다시 열 때 글자가 안 보임)
            asset.atlasTextures[0].name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
            Debug.Log($"[BoldFontAssetCreator] {path} 를 만들었습니다. 실행하면 모든 글자가 이 글꼴로 바뀝니다.");
            Selection.activeObject = asset;
        }

        static Font FindSource()
        {
            foreach (string ext in new[] { ".otf", ".ttf" })
            {
                var f = AssetDatabase.LoadAssetAtPath<Font>($"{SourceFolder}/NotoSansKR-Bold{ext}");
                if (f != null) return f;
            }
            return null;
        }
    }
}
