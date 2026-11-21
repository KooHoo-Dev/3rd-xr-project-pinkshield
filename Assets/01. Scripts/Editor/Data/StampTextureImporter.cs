using UnityEditor;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 정산 도장 그림(Assets/Resources/Stamps)의 가져오기 설정을 자동으로 맞춥니다 (플레이피드백_11-20_설계 5-1).
    /// Sprite · 알파 투명 · 최대 512 · 밉맵 켬(VR에서 멀리 볼 때 반짝임 방지). 그림을 바꿔 넣어도 같은 설정.
    /// 그림 만들기: DevLog/tools/stamp-ink.ps1 (흰 종이 위 검은 선 → 흰 선 + 투명)
    /// </summary>
    public sealed class StampTextureImporter : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/Stamps/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            t.alphaIsTransparency = true;
            t.mipmapEnabled = true;
            t.maxTextureSize = 512;
        }
    }
}
