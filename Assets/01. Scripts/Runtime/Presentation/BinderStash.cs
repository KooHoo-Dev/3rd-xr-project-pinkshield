using System.Collections.Generic;

namespace CardShop.Presentation
{
    /// <summary>
    /// 결과함의 "보관함에 넣기"를 바인더에 잇는 연결부 (보관함 = 바인더, 시연 피드백 B2).
    /// 바인더는 집에, 더미는 가게 카운터에 있어서 카드가 바인더까지 날아갈 수는 없습니다.
    /// 그래서 가게에서는 기존 연출(더미가 위로 날아가 사라짐)을 그대로 쓰고,
    /// 어떤 카드를 넣었는지를 바인더에 알려 두었다가 집에 도착했을 때 그 칸들이 반짝이게 합니다.
    /// 데이터(Collection)는 개봉할 때 이미 반영돼 있어 여기서 바꾸지 않습니다.
    /// </summary>
    public sealed class BinderStash : ICardStash
    {
        readonly BinderView binder;

        /// <summary>실제 연출을 맡는 원래 보관함(기본: FlyAwayCardStash).</summary>
        public ICardStash Inner { get; }

        public BinderStash(BinderView binder, ICardStash inner)
        {
            this.binder = binder;
            Inner = inner;
        }

        public float Send(IReadOnlyList<CardRevealView> pile)
        {
            var ids = new List<string>(pile.Count);
            foreach (CardRevealView card in pile)
                if (card != null && card.Card != null) ids.Add(card.Card.id);
            binder.MarkStored(ids);
            return Inner.Send(pile);
        }
    }
}
