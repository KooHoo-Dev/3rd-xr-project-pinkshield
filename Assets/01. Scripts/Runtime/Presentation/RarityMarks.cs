using CardShop.Cards;
using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>
    /// 등급 마크 = 슬라임 돔 (카드 캔버스 v2): 노말 은색 1 · 레어 파랑 2 · 수퍼레어 보라 3 · 유니크 금색 1 + 반짝임 · 레전더리 무지개 1 + 반짝임.
    /// 카드 앞면(CardView → CardFront 셰이더)과 UI(팩 결과판 칩)가 같은 값을 쓰도록 한 곳에 둡니다 (11-22 "카드 문양 기준").
    /// </summary>
    public static class RarityMarks
    {
        static readonly int[] Counts = { 1, 2, 3, 1, 1 };
        static readonly Color[] Colors =
        {
            new Color(0.66f, 0.68f, 0.72f), new Color(0.35f, 0.65f, 0.94f), new Color(0.65f, 0.48f, 0.94f),
            new Color(0.94f, 0.71f, 0.16f), Color.white,
        };

        static int Index(CardRarity r) => Mathf.Clamp((int)r, 0, Counts.Length - 1);

        public static int Count(CardRarity r) => Counts[Index(r)];
        /// <summary>돔 색 (레전더리는 무지개라 흰색 = 무지개 그림 그대로)</summary>
        public static Color Tint(CardRarity r) => Colors[Index(r)];
        public static bool Rainbow(CardRarity r) => r == CardRarity.Legendary;
        public static bool Sparkle(CardRarity r) => r >= CardRarity.Unique;
    }
}
