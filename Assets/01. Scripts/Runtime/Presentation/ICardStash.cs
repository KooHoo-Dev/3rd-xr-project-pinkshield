using System.Collections.Generic;

namespace CardShop.Presentation
{
    /// <summary>
    /// "보관함에 넣기" 연결 지점 (시연 피드백 B2, 보관함 = 바인더).
    /// 카드 데이터는 개봉 시점에 이미 보유 목록(Collection)에 들어가 있으므로, 여기서는 **연출만** 합니다.
    /// 바인더가 생기면 이 인터페이스를 구현해 <see cref="CardRevealer.Stash"/>에 넣으면 됩니다.
    /// 그 전에는 <see cref="FlyAwayCardStash"/>(더미가 위로 날아가 사라짐)가 기본입니다.
    /// </summary>
    public interface ICardStash
    {
        /// <summary>
        /// 더미 카드들을 보관함으로 보내는 연출을 시작하고, 끝날 때까지 걸리는 시간(초)을 돌려줍니다.
        /// 그 시간이 지나면 CardRevealer가 카드 오브젝트를 숨기고 풀로 거둬 갑니다(구현 쪽에서 숨기거나 파괴하지 말 것).
        /// </summary>
        /// <param name="pile">아래 카드부터 순서대로</param>
        float Send(IReadOnlyList<CardRevealView> pile);
    }
}
