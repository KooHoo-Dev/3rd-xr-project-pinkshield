using CardShop.Cards;
using UnityEngine;

namespace CardShop.Logic
{
    /// <summary>
    /// 중고 장터 (컴퓨터 3단계, 경제시스템_설계 14-3, DEC-040 U2): 카드 1장을 올려 두면 밤사이 팔릴 수 있습니다.
    /// - 올리면 보유 목록에서 빠짐(도감 유지), 내리면 돌아옴. 판매 등록할 수 있는 등급(수퍼레어 이상)만.
    /// - 밤(DayEnd)에 판정: 등급 구매 확률 × onlineStallBuyFactor. 값 = 내일 시세 손님 값 × (1 − onlineStallCommission), 바로 지갑으로.
    /// 순수 C# (OnlineStallRulesTests).
    /// </summary>
    public sealed class OnlineStallRules
    {
        readonly EconomyConfig economy;
        readonly RarityTable rarity;
        readonly EconomyRules prices;
        readonly CardDatabase cards;
        readonly IRandomSource random;

        public OnlineStallRules(EconomyConfig economy, RarityTable rarity, EconomyRules prices, CardDatabase cards, IRandomSource random)
        {
            this.economy = economy; this.rarity = rarity; this.prices = prices; this.cards = cards; this.random = random;
        }

        public bool CanPut(CardData card) => card != null && (int)card.rarity >= (int)economy.minDisplayRarity;

        /// <summary>카드를 장터에 올림. 이미 올린 카드가 있으면 먼저 보유 목록으로 돌려놓고 바꿈.</summary>
        public bool Put(GameSession s, string cardId)
        {
            CardData card = cards.Get(cardId);
            if (!CanPut(card) || s.Collection.CountOf(cardId) <= 0) return false;
            Take(s);
            s.Collection.Remove(cardId);
            s.OnlineStall = cardId;
            return true;
        }

        /// <summary>장터에서 내림 (보유 목록으로). 올린 카드가 없으면 false</summary>
        public bool Take(GameSession s)
        {
            if (s.OnlineStall == null) return false;
            s.Collection.Add(s.OnlineStall);
            s.OnlineStall = null;
            return true;
        }

        /// <summary>장터 값 (수수료 뒤)</summary>
        public int Price(CardData card, MarketDay market) =>
            Mathf.Max(1, Mathf.FloorToInt(prices.GrossPrice(card, market) * (1f - economy.onlineStallCommission) + 1e-4f));

        public float SellChance(CardData card) =>
            Mathf.Clamp01(rarity.Get(card.rarity).customerBuyChance * economy.onlineStallBuyFactor);

        /// <summary>밤사이 판정. 팔리면 장터를 비우고 돈을 지갑에 넣고 true (카드 · 금액을 돌려줌).</summary>
        public bool ResolveNight(GameSession s, MarketDay market, out CardData sold, out int net)
        {
            sold = null;
            net = 0;
            CardData card = s.OnlineStall != null ? cards.Get(s.OnlineStall) : null;
            if (card == null) return false;
            if (random.NextDouble() >= SellChance(card)) return false;
            net = Price(card, market);
            s.OnlineStall = null;
            s.Wallet.Add(net);
            sold = card;
            return true;
        }
    }
}
