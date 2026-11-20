using TMPro;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 글자를 한 글자씩 보여 줍니다 (UI_개편_설계 3번 판: 손님 말풍선, Animal Crossing 대사 참고).
    /// TMP maxVisibleCharacters만 바꾸므로 글자 배치 · 말풍선 크기는 처음부터 다 쓴 상태 그대로입니다 (말풍선이 커지며 흔들리지 않음).
    /// 시간은 부른 쪽이 넘겨 줌 → 일시정지 중에는 멈춤.
    /// </summary>
    public sealed class TypewriterText
    {
        /// <summary>1초에 보여 주는 글자 수</summary>
        public const float CharsPerSecond = 30f;

        readonly TMP_Text text;
        float shown;
        int total;

        public TypewriterText(TMP_Text text) { this.text = text; }

        public bool Done => shown >= total;

        /// <summary>글자를 넣은 직후(배치가 끝난 뒤) 부름. 0글자부터 시작</summary>
        public void Restart()
        {
            text.ForceMeshUpdate();
            total = text.textInfo.characterCount;
            shown = 0f;
            text.maxVisibleCharacters = 0;
        }

        public void Tick(float dt)
        {
            if (Done) return;
            shown = Mathf.Min(total, shown + dt * CharsPerSecond);
            text.maxVisibleCharacters = VisibleCount(shown, total);
        }

        /// <summary>지금까지 흐른 글자 수 → 보이는 글자 수 (순수, 테스트)</summary>
        public static int VisibleCount(float shown, int total) => Mathf.Clamp(Mathf.FloorToInt(shown), 0, total);

        public void Finish()
        {
            shown = total;
            text.maxVisibleCharacters = 99999;
        }
    }
}
