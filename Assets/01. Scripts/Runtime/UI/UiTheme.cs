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

        // ---- C안 "장소마다 다른 재질" (UI_개편_설계, 11-20): 위 색은 그대로 두고 재질 색을 더함 ----

        /// <summary>버튼 중 가장 중요한 것 (출근하기 · 이어서 하기)</summary>
        public static readonly Color ButtonPrimary = new Color(0.93f, 0.36f, 0.6f);
        public static readonly Color ButtonPrimaryHover = new Color(1f, 0.55f, 0.75f);
        /// <summary>버튼 아래 두께 (그림자) — 누르는 물건처럼 보이게</summary>
        public static readonly Color ButtonDepth = new Color(0.17f, 0.12f, 0.27f, 0.85f);

        /// <summary>종이 (메모 · 클립보드 · 상장 · 알림 쪽지)</summary>
        public static readonly Color Paper = new Color(0.984f, 0.961f, 0.902f);
        /// <summary>영수증 (정산)</summary>
        public static readonly Color Receipt = new Color(1f, 0.996f, 0.973f);
        /// <summary>종이 위 글자 (진보라 잉크)</summary>
        public static readonly Color Ink = new Color(0.169f, 0.129f, 0.251f);
        /// <summary>종이 위 보조 글자</summary>
        public static readonly Color InkSoft = new Color(0.42f, 0.376f, 0.502f);
        /// <summary>클립보드 · 입간판 나무 틀</summary>
        public static readonly Color Kraft = new Color(0.78f, 0.6f, 0.38f);
        public static readonly Color Wood = new Color(0.42f, 0.29f, 0.18f);
        /// <summary>입간판 칠판</summary>
        public static readonly Color Chalkboard = new Color(0.18f, 0.23f, 0.2f);
        /// <summary>클립 금속</summary>
        public static readonly Color Metal = new Color(0.6f, 0.63f, 0.66f);
        /// <summary>도장 · 강조 분홍 (종이 위에서 읽히는 진한 분홍)</summary>
        public static readonly Color Stamp = new Color(0.93f, 0.36f, 0.6f);
        /// <summary>위험한 버튼 (처음부터 다시)</summary>
        public static readonly Color Danger = new Color(0.76f, 0.23f, 0.28f);
        public static readonly Color MoneyPlus = new Color(0.18f, 0.64f, 0.49f);
        public static readonly Color MoneyMinus = new Color(0.85f, 0.26f, 0.31f);
        /// <summary>카드 재질 판 (팩 결과) 바탕</summary>
        public static readonly Color CardBack = new Color(0.231f, 0.169f, 0.349f, 0.96f);

        /// <summary>사장님 말풍선 테두리 · 이름표 (노랑 실선)</summary>
        public static readonly Color BossLine = new Color(0.96f, 0.76f, 0.2f);
        /// <summary>혼잣말 말풍선 테두리 · 이름표 (하늘색 점선)</summary>
        public static readonly Color SelfLine = new Color(0.45f, 0.74f, 0.95f);

        /// <summary>3D 글자 뒤 띠 (&lt;mark=…&gt;). 판 바탕보다 조금 어두운 보라, 알파 CC</summary>
        public const string Mark = "#3B2B59CC";
        /// <summary>혼잣말 · 손목 재화처럼 더 진하게 가려야 하는 띠</summary>
        public const string MarkStrong = "#3B2B59F0";
    }
}
