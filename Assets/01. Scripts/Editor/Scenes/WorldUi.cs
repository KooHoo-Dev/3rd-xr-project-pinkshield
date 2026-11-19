using CardShop.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 월드 공간 UI 조각(버튼 · 글자 · 그림)을 만드는 빌더 공용 함수.
    /// 실제 만드는 코드는 런타임 화면과 같이 쓰도록 <see cref="WorldUiKit"/>에 있고, 여기는 기존 빌더 호출을 그대로 잇기만 합니다.
    /// </summary>
    internal static class WorldUi
    {
        public static void Stretch(RectTransform rt, Vector2 min, Vector2 max) => WorldUiKit.Stretch(rt, min, max);

        public static Image AddImage(Transform parent, string name, Vector2 min, Vector2 max, Color color) =>
            WorldUiKit.AddImage(parent, name, min, max, color);

        /// <summary>레이를 받지 않는 글자 (뒤의 버튼 클릭을 가리지 않게).</summary>
        public static TMP_Text AddText(Transform parent, string name, string text, Vector2 min, Vector2 max, float size, Color color, TMP_FontAsset font) =>
            WorldUiKit.AddText(parent, name, text, min, max, size, color, font);

        /// <summary>테마 판 바탕 + 위쪽 분홍 띠(높이 accentMm). 바탕을 돌려줌 (레이는 받지 않음).</summary>
        public static Image AddPanel(Transform parent, float accentMm = 8f) => WorldUiKit.AddPanel(parent, accentMm);

        public static Button AddButton(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax, TMP_FontAsset font) =>
            WorldUiKit.AddButton(parent, text, anchorMin, anchorMax, font);
    }
}
