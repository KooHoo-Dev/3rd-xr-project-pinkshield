using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// UI 색 한 곳 (DEC-027: 테마 A "카드샵 굿즈" — 진보라 바탕 + 분홍 · 금색 포인트).
    /// 빌더(WorldUi · 판 빌더)와 런타임 TMP 3D 띠(&lt;mark&gt;)가 같은 값을 씁니다. 여기만 바꾸면 전체가 바뀝니다(씬 다시 만들기 필요).
    /// 밝은 낮 가게에서 판이 무거워 보이는 단점 보정: 검정에 가까운 보라 대신 한 단계 밝은 보라 바탕, 순백 대신 크림색 글자,
    /// 판 위쪽에 분홍 띠, 버튼은 바탕보다 밝은 보라로 떠 보이게.
    /// </summary>
    public static class UiTheme
    {
        /// <summary>판 바탕 (판매 · 바인더 · 정산 · 타이틀 · 일시정지 · 디버그)</summary>
        public static readonly Color Panel = new Color(0.29f, 0.22f, 0.45f, 0.95f);
        /// <summary>판 위쪽 분홍 띠</summary>
        public static readonly Color Accent = new Color(1f, 0.55f, 0.75f);
        /// <summary>제목 · 강조 (금색)</summary>
        public static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        /// <summary>본문 글자 (크림)</summary>
        public static readonly Color Text = new Color(1f, 0.97f, 0.92f);
        /// <summary>보조 글자 (연보라)</summary>
        public static readonly Color SubText = new Color(0.8f, 0.76f, 0.92f);

        public static readonly Color ButtonNormal = new Color(0.46f, 0.34f, 0.7f);
        /// <summary>레이가 닿았을 때. 바탕과 확 달라야 보임 (시연 피드백 C3)</summary>
        public static readonly Color ButtonHover = new Color(0.93f, 0.36f, 0.6f);
        public static readonly Color ButtonPressed = new Color(0.68f, 0.2f, 0.42f);
        public static readonly Color ButtonDisabled = new Color(0.3f, 0.27f, 0.36f, 0.6f);

        /// <summary>3D 글자 뒤 띠 (&lt;mark=…&gt;). 판 바탕보다 조금 어두운 보라, 알파 CC</summary>
        public const string Mark = "#3B2B59CC";
        /// <summary>혼잣말 · 손목 재화처럼 더 진하게 가려야 하는 띠</summary>
        public const string MarkStrong = "#3B2B59F0";
    }
}
