using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>
    /// 판매 진열과 손님 구매 (기획서 5-4, 설계서 Q8 · Q10).
    /// - 진열하면 카드가 보유 목록에서 빠져 진열대로 갑니다. 도감은 그대로 유지됩니다(Q9).
    /// - 손님은 진열 카드 중 1장을 균등하게 고르고, 등급별 구매 확률로 삽니다. 못 사면 가게 재고를 삽니다(수입 없음).
    /// - 안 팔린 카드는 하루가 끝날 때 보유 목록으로 돌아옵니다.
    /// </summary>
    public sealed class SalesRules
    {
        readonly EconomyConfig economy;
        readonly RarityTable rarity;
        readonly EconomyRules prices;
        readonly CardDatabase cards;
        readonly IRandomSource random;

        public SalesRules(EconomyConfig economy, RarityTable rarity, EconomyRules prices, CardDatabase cards, IRandomSource random)
        {
            this.economy = economy;
            this.rarity = rarity;
            this.prices = prices;
            this.cards = cards;
            this.random = random;
        }

        public bool HasFreeSlot(GameSession s) => s.SalesDisplay.Count < economy.salesDisplaySlots;

        /// <summary>보유한 카드를 진열합니다. 빈 칸이 없거나 보유하지 않았으면 false.</summary>
        public bool TryDisplay(GameSession s, string cardId)
        {
            if (!HasFreeSlot(s) || cards.Get(cardId) == null || !s.Collection.Remove(cardId)) return false;
            s.SalesDisplay.Add(cardId);
            return true;
        }

        /// <summary>진열을 취소하고 보유 목록으로 되돌립니다.</summary>
        public bool TryUndisplay(GameSession s, string cardId)
        {
            if (!s.SalesDisplay.Remove(cardId)) return false;
            s.Collection.Add(cardId);
            return true;
        }

        /// <summary>
        /// 손님 한 명의 구매. 진열 카드를 샀으면 그 카드를, 가게 재고를 샀으면 null을 돌려줍니다.
        /// 판매 내역은 당일 장부에 기록되어 정산 때 합산됩니다.
        /// </summary>
        public CardData ServeCustomer(GameSession s, CardElement popular)
        {
            s.Ledger.customersServed++;
            if (s.SalesDisplay.Count == 0) return null;

            int index = random.Range(0, s.SalesDisplay.Count);
            CardData card = cards.Get(s.SalesDisplay[index]);
            if (card == null || random.NextDouble() >= rarity.Get(card.rarity).customerBuyChance) return null;

            s.SalesDisplay.RemoveAt(index);
            s.Ledger.sales.Add(new DayLedger.Sale
            {
                cardId = card.id,
                gross = prices.GrossPrice(card, popular),
                net = prices.SellPrice(card, popular),
            });
            return card;
        }

        /// <summary>하루가 끝날 때 안 팔린 진열 카드를 보유 목록으로 돌려놓습니다. 돌려놓은 장수를 돌려줍니다.</summary>
        public int ReturnUnsold(GameSession s)
        {
            int n = s.SalesDisplay.Count;
            foreach (string id in s.SalesDisplay) s.Collection.Add(id);
            s.SalesDisplay.Clear();
            return n;
        }
    }
}
