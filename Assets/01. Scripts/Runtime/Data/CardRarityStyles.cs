using System;
using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>
    /// 등급별 카드 외형 (기획서 5-6 희귀 카드 셰이더) + 속성 색.
    /// 셰이더 수치를 데이터로 분리해 에디터에서 바로 조정합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Card Rarity Styles", fileName = "CardRarityStyles")]
    public class CardRarityStyles : ScriptableObject
    {
        [Serializable]
        public class Style
        {
            public string label = "노말";
            public Color frameColor = new Color(0.78f, 0.78f, 0.80f);
            public Color accentColor = new Color(0.6f, 0.6f, 0.65f);

            [Tooltip("배경이 카드 전체를 채움 (유니크·레전더리)")]
            public bool fullArt;
            [Tooltip("캐릭터가 프레임 밖으로 튀어나옴 (레전더리)")]
            public bool popOut;

            [Tooltip("캐릭터 크기: 일러스트 창의 짧은 변 대비 배율")]
            public float charScale = 1f;
            [Tooltip("캐릭터 세로 위치: 일러스트 창 높이 대비 (+ 위쪽)")]
            public float charOffsetY;

            [Range(0, 1)] public float frameSparkle;
            [Range(0, 1)] public float artHolo;
            [Range(0, 1)] public float scrollPattern;
            [Range(0, 1)] public float rim;
            [Range(0, 2)] public float glow;
        }

        [Tooltip("CardRarity 순서: 노말, 레어, 수퍼레어, 유니크, 레전더리")]
        public Style[] styles = new Style[5];

        [Tooltip("CardElement 순서: None, 불, 물, 땅, 공기, 무속성")]
        public Color[] elementColors = new Color[6];

        public Style Get(CardRarity rarity)
        {
            int i = (int)rarity;
            return styles != null && i < styles.Length && styles[i] != null ? styles[i] : new Style();
        }

        public Color GetElementColor(CardElement element)
        {
            int i = (int)element;
            return elementColors != null && i < elementColors.Length ? elementColors[i] : Color.gray;
        }
    }
}
