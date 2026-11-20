using TMPro;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 종이 말풍선 = 글자 + 종이 판(TextPlate, 테두리 실선 / 점선) + 왼쪽 위 이름표(NameTag). UI_개편_설계 9번 판.
    /// 사장님 · 혼잣말 말풍선(BossBubble)과 튜토리얼 자막(TutorialCaption)이 같이 씁니다. 예전 진보라 &lt;mark&gt; 띠를 대신함.
    /// </summary>
    public sealed class PaperBubble : MonoBehaviour
    {
        const float FrameM = 0.004f;

        TextMeshPro text;
        TextPlate plate;
        NameTag nameTag;

        /// <param name="text">말풍선 글자 (이 오브젝트에 붙음)</param>
        /// <param name="padding">글자 둘레 여백 (m)</param>
        /// <param name="cornerM">모서리 반지름 (m)</param>
        /// <param name="sortBase">그리는 순서 시작 값. 판 0 · 테두리 1 · 글자 2 · 이름표 3~5를 더함</param>
        public static PaperBubble Attach(TextMeshPro text, Vector2 padding, float cornerM, int sortBase = 0)
        {
            var bubble = text.gameObject.AddComponent<PaperBubble>();
            bubble.text = text;
            bubble.plate = TextPlate.Attach(text, UiTheme.Paper, padding, cornerM, UiTheme.BossLine, FrameM, false, sortBase);
            bubble.nameTag = NameTag.Create(text.transform, text.fontSize * 0.7f, sortBase + 3);
            return bubble;
        }

        /// <summary>말하는 사람 모양으로 칠하고 글자를 넣은 뒤 판 · 이름표를 글자에 맞춥니다</summary>
        public void Show(BubbleStyle style, string richText)
        {
            text.color = UiTheme.Ink;
            text.text = richText;
            plate.SetStyle(UiTheme.Paper, style.Line, style.Dashed);
            nameTag.Set(style.Label, style.Line);
            plate.Fit();
            nameTag.PinTo(plate.Area);
        }

        /// <summary>글자 · 판 · 이름표를 함께 (0~1)</summary>
        public void SetAlpha(float a)
        {
            text.alpha = a;
            plate.SetAlpha(a);
            nameTag.SetAlpha(a);
        }

        /// <summary>물건에 가려지지 않게 맨 위에 (튜토리얼 자막)</summary>
        public void DrawOnTop()
        {
            plate.DrawOnTop();
            nameTag.DrawOnTop();
        }
    }
}
