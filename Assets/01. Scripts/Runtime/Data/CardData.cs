using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>
    /// 카드 1종의 데이터 (기획서 5-7 데이터 테이블).
    /// 원본은 Assets/02. Data/cards.csv 입니다. Tools > CardShop > Import Cards CSV 로 생성·갱신하세요.
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Card Data", fileName = "CardData")]
    public class CardData : ScriptableObject
    {
        [Tooltip("기획서·아트 파일 이름과 같은 ID (예: F-N1)")]
        public string id;

        public CardKind kind;
        public CardElement element;
        public CardRarity rarity;

        public string displayName;
        [TextArea(2, 4)] public string description;

        [Tooltip("기준가. CSV 임포트 시 RarityTable에서 채워집니다 (직접 수정하지 마세요).")]
        public int basePrice = 1;

        [Header("Art")]
        [Tooltip("누끼를 딴 캐릭터 레이어 (정사각형, 투명 배경)")]
        public Texture2D character;

        [Tooltip("배경 레이어. 비워 두면 속성 색 그라데이션을 씁니다.")]
        public Texture2D background;

        [Tooltip("필드 카드 수퍼레어 이상의 전경 레이어 (패럴랙스)")]
        public Texture2D foreground;
    }
}
