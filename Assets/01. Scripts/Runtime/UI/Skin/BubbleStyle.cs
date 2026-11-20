using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 말하는 사람별 말풍선 모양 (UI_개편_설계 9번 판, Animal Crossing의 이름표 말풍선 참고).
    /// 누가 말하는지 이름표 글자 · 색 · 테두리(실선 / 점선)로 바로 구분되게: 사장님 = 노랑 실선, 혼잣말 = 하늘색 점선, 손님 = 분홍.
    /// </summary>
    public readonly struct BubbleStyle
    {
        public readonly string Label;
        /// <summary>테두리 · 이름표 색</summary>
        public readonly Color Line;
        public readonly bool Dashed;

        public BubbleStyle(string label, Color line, bool dashed)
        {
            Label = label;
            Line = line;
            Dashed = dashed;
        }

        public static BubbleStyle Boss => new BubbleStyle("사장님", UiTheme.BossLine, false);
        public static BubbleStyle Self => new BubbleStyle("혼잣말", UiTheme.SelfLine, true);
        public static BubbleStyle Customer => new BubbleStyle("손님", UiTheme.Accent, false);
    }
}
