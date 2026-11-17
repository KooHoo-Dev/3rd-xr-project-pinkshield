using CardShop.Cards;
using UnityEngine;

namespace CardShop.Logic
{
    /// <summary>진열 시도의 결과 (거부 이유를 UI에 알리기 위함).</summary>
    public enum DisplayResult
    {
        Ok,
        UnknownCard,
        RarityTooLow,
        DisplayFull,
        NotOwned,
    }

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

        /// <summary>진열할 수 있는 등급인가 (EconomyConfig.minDisplayRarity 이상, 기본 수퍼레어).</summary>
        public bool CanDisplay(CardData card) => card != null && (int)card.rarity >= (int)economy.minDisplayRarity;

        /// <summary>보유한 카드를 진열합니다. 결과로 거부 이유를 알 수 있습니다.</summary>
        public DisplayResult Display(GameSession s, string cardId)
        {
            CardData card = cards.Get(cardId);
            if (card == null) return DisplayResult.UnknownCard;
            if (!CanDisplay(card)) return DisplayResult.RarityTooLow;
            if (!HasFreeSlot(s)) return DisplayResult.DisplayFull;
            if (!s.Collection.Remove(cardId)) return DisplayResult.NotOwned;
            s.SalesDisplay.Add(cardId);
            return DisplayResult.Ok;
        }

        /// <summary>보유한 카드를 진열합니다. 등급이 낮거나 빈 칸이 없거나 보유하지 않았으면 false.</summary>
        public bool TryDisplay(GameSession s, string cardId) => Display(s, cardId) == DisplayResult.Ok;

        /// <summary>즉시 판매가: 기준가 × instantSellRate, 소수점 내림, 최소 1. 수수료 · 인기 속성 배율 없음(고정 가격).</summary>
        public int InstantPrice(CardData card) =>
            Mathf.Max(1, Mathf.FloorToInt(rarity.BasePrice(card.rarity) * economy.instantSellRate + 1e-4f));

        /// <summary>
        /// 보유한 카드를 집에서 바로 팝니다(모든 등급). 카드는 보유 목록에서 1장 빠지고(도감은 유지)
        /// 값은 곧바로 지갑에 들어갑니다. 하루 장부에는 적지 않습니다. 보유하지 않았으면 false.
        /// </summary>
        public bool TrySellInstantly(GameSession s, string cardId, out int price)
        {
            price = 0;
            CardData card = cards.Get(cardId);
            if (card == null || !s.Collection.Remove(cardId)) return false;
            price = InstantPrice(card);
            s.Wallet.Add(price);
            return true;
        }

        /// <summary>마지막 날 끝에서만 안 팔린 진열 카드를 돌려놓습니다(최종 점수에 카드 가치가 들어가므로). 그 외의 날은 집에서 등록한 카드를 지우지 않도록 아무것도 안 합니다.</summary>
        public int ReturnUnsoldOnFinalDay(GameSession s, int dayCount) =>
            s.Day >= dayCount ? ReturnUnsold(s) : 0;

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
