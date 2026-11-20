using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 말하는 사람별 말풍선 모양 (UI_개편_설계 9번 판, Animal Crossing의 이름표 말풍선 참고).
    /// 누가 말하는지 이름표 글자 · 색 · 테두리(실선 / 점선)로 바로 구분되게: 사장님 = 노란 이름표 + 잉크 실선, 혼잣말 = 하늘색 점선 생각 풍선, 손님 = 분홍 이름표 + 잉크 실선 (11-20 샘플 적용).
    /// </summary>
    public readonly struct BubbleStyle
    {
        public readonly string Label;
        /// <summary>테두리 색 (사장님 · 손님 = 잉크 실선, 혼잣말 = 하늘색 점선 — UI 개편 샘플 9번)</summary>
        public readonly Color Line;
        public readonly bool Dashed;
        /// <summary>말풍선 바탕</summary>
        public readonly Color Fill;
        /// <summary>이름표 바탕 (테두리는 Line)</summary>
        public readonly Color TagFill;

        public BubbleStyle(string label, Color line, bool dashed, Color fill, Color tagFill)
        {
            Label = label;
            Line = line;
            Dashed = dashed;
            Fill = fill;
            TagFill = tagFill;
        }

        static readonly Color SelfFill = new Color(0.906f, 0.925f, 1f);      // #E7ECFF 생각 풍선
        static readonly Color SelfDash = new Color(0.478f, 0.525f, 0.722f);  // #7A86B8
        static readonly Color SelfTag = new Color(0.82f, 0.847f, 1f);        // #D1D8FF

        public static BubbleStyle Boss => new BubbleStyle("사장님", UiTheme.SoftInkLine, false, UiTheme.SoftPaper, UiTheme.SoftGold);
        public static BubbleStyle Self => new BubbleStyle("혼잣말", SelfDash, true, SelfFill, SelfTag);
        public static BubbleStyle Customer => new BubbleStyle("손님", UiTheme.SoftInkLine, false, UiTheme.SoftPaper, UiTheme.SoftPink);
    }
}
