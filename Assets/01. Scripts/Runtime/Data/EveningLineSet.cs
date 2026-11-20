using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>
    /// 퇴근 혼잣말 목록. 아래 칸에 "상황|문장" 한 줄씩 붙여 넣습니다. 비어 있는 상황은 코드(EveningLines)의 기본 문장을 씁니다.
    /// 상황: WorkPlain · WorkBusy · WorkQuiet · WorkClean · WorkMessy · WorkLost · WorkThief · WorkRobbed · MoneyBig · MoneySold · MoneyUnsold · PackNone · PackPlain · PackNew · PackSuperRare · PackUnique · PackLegendary · PackGodPack
    /// 바뀌는 숫자: {customers} 받은 손님 · {cleanups} 한 청소 · {new} 새 카드 · {packs} 연 팩 · {day} 일차
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Evening Line Set", fileName = "EveningLineSet")]
    public class EveningLineSet : ScriptableObject
    {
        [TextArea(12, 60)]
        [Tooltip("한 줄에 상황|문장. 빈 줄과 # 로 시작하는 줄은 무시")]
        public string lines = "# 예) WorkBusy|눈코 뜰 새 없었다. 물 한 모금도 못 마셨어.\n";
    }
}
