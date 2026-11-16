using UnityEditor;

namespace CardShop.Cards.EditorTools
{
    /// <summary>
    /// Assets/04. Art/Cards/ 아래 카드 이미지의 임포트 설정을 자동으로 맞춥니다.
    /// (투명 배경, Clamp, 1024px, VR 축소 대비 밉맵)
    /// </summary>
    public class CardTextureImporter : AssetPostprocessor
    {
        const string Root = "Assets/04. Art/Cards/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root) || assetPath.Contains("/Source/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
        }
    }
}
