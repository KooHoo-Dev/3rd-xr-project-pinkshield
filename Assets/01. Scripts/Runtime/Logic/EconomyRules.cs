using CardShop.Cards;
using UnityEngine;

namespace CardShop.Logic
{
    /// <summary>판매가와 일일 정산 계산 (기획서 5-4, 5-5).</summary>
    public sealed class EconomyRules
    {
        readonly EconomyConfig config;
        readonly RarityTable rarity;

        public EconomyRules(EconomyConfig config, RarityTable rarity)
        {
            this.config = config;
            this.rarity = rarity;
        }

        /// <summary>인기 속성 배율 적용 전·수수료 전 가격.</summary>
        public int GrossPrice(CardData card, CardElement popular)
        {
            float mult = IsPopular(card, popular) ? config.popularMultiplier : 1f;
            return Mathf.FloorToInt(rarity.BasePrice(card.rarity) * mult);
        }

        /// <summary>판매 수령액 = max(최소가, 내림(기준가 × 배율 × (1 − 수수료)))</summary>
        public int SellPrice(CardData card, CardElement popular)
        {
            float mult = IsPopular(card, popular) ? config.popularMultiplier : 1f;
            int net = Mathf.FloorToInt(rarity.BasePrice(card.rarity) * mult * (1f - config.commissionRate) + 1e-4f);
            return Mathf.Max(config.minSellPrice, net);
        }

        /// <summary>인기 속성은 슬라임 카드에만 적용 (필드·간식 제외).</summary>
        public bool IsPopular(CardData card, CardElement popular) =>
            config.popularElementEnabled && card.kind == CardKind.Slime && popular != CardElement.None && card.element == popular;

        public int CleanupTotal(DayLedger ledger) =>
            ledger.cleanupsDone * config.cleanupBonus - ledger.cleanupsMissed * config.cleanupPenalty;

        /// <summary>계산 평가: 빠른 계산 보너스 − 떠난 손님 감점 (계산시스템_확장_설계 3장).</summary>
        public int CheckoutTotal(DayLedger ledger) =>
            ledger.checkoutsFast * config.checkoutBonus - ledger.customersLost * config.checkoutPenalty;

        /// <summary>물건을 올린 뒤 결제까지 걸린 시간이 기준 안인가. 기준 = 기본 + (물건 수 − 1) × 추가 시간.</summary>
        public bool IsFastCheckout(float seconds, int itemCount) =>
            seconds <= config.checkoutFastSeconds + config.checkoutFastPerExtraItemSeconds * Mathf.Max(0, itemCount - 1);

        public int SalesTotal(DayLedger ledger)
        {
            int sum = 0;
            foreach (DayLedger.Sale s in ledger.sales) sum += s.net;
            return sum;
        }

        /// <summary>기본급 + 청소 보너스 − 감점 + 계산 평가 + 판매 수익. 음수가 되지 않습니다.</summary>
        public int SettlementTotal(DayLedger ledger) =>
            Mathf.Max(0, ledger.basePay + CleanupTotal(ledger) + CheckoutTotal(ledger) + SalesTotal(ledger));
    }
}
