using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>
    /// 손님 말풍선 대사 목록 (2026-11-18 기기 테스트 피드백: 손님이 말을 안 해서 단조로움). "상황|문장" 한 줄씩 붙여 넣습니다.
    /// 상황: Enter · Browse · Waiting · Impatient · Checkout · PaidCard · Paid · Angry (CustomerMoment).
    /// 비어 있는 상황은 코드(CustomerLines)의 기본 문장을 씁니다. {card}는 손님이 산 판매 등록 카드 이름으로 바뀝니다.
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Customer Line Set", fileName = "CustomerLineSet")]
    public class CustomerLineSet : ScriptableObject
    {
        [TextArea(12, 60)]
        [Tooltip("한 줄에 상황|문장. 빈 줄과 # 로 시작하는 줄은 무시. 상황 이름은 대소문자 무시")]
        public string lines = "# 예) Enter|여기 카드 맛집이라던데?\n# 예) PaidCard|{card} 드디어 손에 넣었다!\n";

        [Header("말할 확률 (0~1). 계산 · 결제 · 화남은 늘 말함")]
        [Range(0f, 1f)] public float enterChance = 0.5f;
        [Range(0f, 1f)] public float browseChance = 0.6f;
        [Range(0f, 1f)] public float waitingChance = 0.5f;

        [Header("말풍선")]
        [Tooltip("말풍선이 떠 있는 시간(초)")]
        public float bubbleSeconds = 2.8f;
        [Tooltip("인내 시간의 이 비율이 지나면 '기다리는 중' 대사 (Waiting)")]
        [Range(0f, 1f)] public float waitingAt = 0.35f;
        [Tooltip("인내 시간의 이 비율이 지나면 '재촉' 대사 (Impatient). 손님이 곧 떠난다는 신호")]
        [Range(0f, 1f)] public float impatientAt = 0.75f;
    }
}
