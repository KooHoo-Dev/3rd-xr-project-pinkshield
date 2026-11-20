using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>
    /// 아침 혼잣말 목록. 아래 칸에 "기분|문장" 한 줄씩 붙여 넣습니다 (생성 요령: 98. Doc/아침대사_생성_가이드.md).
    /// 기분: Plain · NoPacks · SuperRare · Unique · Legendary · LastDay · GodPack · Endless(무한 모드) · DisplayOne · DisplayMany(둘째 줄: 진열대에 내 카드가 있을 때). 비어 있는 기분은 코드의 기본 문장을 씁니다.
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Morning Line Set", fileName = "MorningLineSet")]
    public class MorningLineSet : ScriptableObject
    {
        [TextArea(12, 60)]
        [Tooltip("한 줄에 기분|문장. 빈 줄과 # 로 시작하는 줄은 무시. {day}는 일차 숫자로 바뀜")]
        public string lines = "# 예) Legendary|어제 레전더리라니… 오늘도 {day}일차, 힘내자!\n";
    }
}
