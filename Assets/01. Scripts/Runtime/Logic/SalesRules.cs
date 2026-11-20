using System.Collections.Generic;
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
    /// - 손님은 진열 카드를 모두 둘러보고 1장까지 삽니다(PickPurchase). 못 사면 가게 재고를 삽니다(수입 없음).
    /// - 안 팔린 카드는 하루가 끝날 때 보유 목록으로 돌아옵니다.
    /// </summary>
    public sealed class SalesRules
    {
        readonly EconomyConfig economy;
        readonly RarityTable rarity;
        readonly EconomyRules prices;
        readonly CardDatabase cards;
        readonly IRandomSource random;
        readonly UpgradeRules upgrades; // 바인더 업그레이드: 판매 등록 칸 · 즉시 판매 비율 (없으면 기본값)

        public SalesRules(EconomyConfig economy, RarityTable rarity, EconomyRules prices, CardDatabase cards, IRandomSource random,
            UpgradeRules upgrades = null)
        {
            this.upgrades = upgrades;
            this.economy = economy;
            this.rarity = rarity;
            this.prices = prices;
            this.cards = cards;
            this.random = random;
        }

        /// <summary>판매 등록 칸 수 = 기본 + 바인더 업그레이드 (집_업그레이드_설계 3장)</summary>
        public int SlotCount(GameSession s) =>
            economy.salesDisplaySlots + (upgrades != null && s != null ? upgrades.ExtraSalesSlots(s.Upgrades) : 0);

        public bool HasFreeSlot(GameSession s) => s.SalesDisplay.Count < SlotCount(s);

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

        /// <summary>
        /// 매입가 1장 값 (소수 그대로): 등급 매입가 × (1 + 바인더 보너스). 인기 속성 · 수수료와 무관 (경제시스템_설계 5장, DEC-041).
        /// 노멀은 0.2라 1장만으로는 0 → "모두 팔기"에서 합계를 낸 뒤 내림합니다.
        /// </summary>
        public float InstantValue(CardData card, GameSession s = null) =>
            rarity.BuybackPrice(card.rarity) * (1f + (upgrades != null && s != null ? upgrades.InstantSellBonus(s.Upgrades) : 0f));

        /// <summary>낱장 즉시 판매가 (매입가 내림). 0이면 낱장으로는 못 팖.</summary>
        public int InstantPrice(CardData card, GameSession s = null) =>
            Mathf.Max(0, Mathf.FloorToInt(InstantValue(card, s) + 1e-4f));

        /// <summary>
        /// 보유한 카드를 집에서 바로 팝니다(매입가). 카드는 보유 목록에서 1장 빠지고(도감은 유지)
        /// 값은 곧바로 지갑에 들어갑니다. 하루 장부에는 적지 않습니다. 보유하지 않았거나 낱장 값이 0(노멀)이면 false.
        /// </summary>
        public bool TrySellInstantly(GameSession s, string cardId, out int price)
        {
            price = 0;
            CardData card = cards.Get(cardId);
            if (card == null || InstantPrice(card, s) <= 0 || !s.Collection.Remove(cardId)) return false;
            price = InstantPrice(card, s);
            s.Wallet.Add(price);
            return true;
        }

        /// <summary>
        /// 한꺼번에 팔기 견적 (11-19 피드백 H: 레어 이하 모두 팔기). maxRarity 이하 등급의 보유 카드 전부(장수 포함)를
        /// 즉시 판매가로 팔면 몇 장 · 얼마인지. 아무것도 바꾸지 않습니다.
        /// </summary>
        public int QuoteSellAllUpTo(GameSession s, CardRarity maxRarity, out int total)
        {
            int count = 0;
            float sum = 0f;
            foreach (KeyValuePair<string, int> e in s.Collection.Owned)
            {
                CardData card = cards.Get(e.Key);
                if (card == null || card.rarity > maxRarity) continue;
                count += e.Value;
                sum += InstantValue(card, s) * e.Value;
            }
            total = Mathf.FloorToInt(sum + 1e-4f); // 노멀 0.2 × 13장 = 2.6 → 2
            return count;
        }

        /// <summary>maxRarity 이하 등급의 보유 카드를 모두 즉시 판매합니다(도감은 유지). 판 장수를 돌려주고 합계는 total.</summary>
        public int SellAllUpTo(GameSession s, CardRarity maxRarity, out int total)
        {
            int count = 0;
            float sum = 0f;
            var ids = new List<string>(s.Collection.Owned.Keys); // 파는 동안 목록이 바뀌므로 먼저 복사
            foreach (string id in ids)
            {
                CardData card = cards.Get(id);
                if (card == null || card.rarity > maxRarity) continue;
                float value = InstantValue(card, s);
                while (s.Collection.Remove(id))
                {
                    count++;
                    sum += value;
                }
            }
            total = Mathf.FloorToInt(sum + 1e-4f); // 합계를 낸 뒤 내림 (노멀 묶음 판매)
            if (total > 0) s.Wallet.Add(total);
            return count;
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
        /// 1단계: 손님이 계산대에 가져올 물건을 고릅니다 (경제시스템_설계 13-2 · 8장, DEC-041 Q5 · Q8).
        /// 손님은 진열 카드를 모두 둘러보고(인기 카드를 먼저 볼 가능성이 큼), 카드마다
        /// "등급 구매 확률 × browseBuyFactor × 인기 배율"로 판정해 처음 통과한 1장을 삽니다. 모두 탈락하면 null(가게 재고).
        /// 그래서 판매 칸이 많을수록 더 잘 팔립니다. 아직 진열대 · 장부는 바꾸지 않습니다
        /// (바코드를 찍기 전에 손님이 기다리다 떠나면 판매가 없어야 하므로).
        /// </summary>
        public CardData PickPurchase(GameSession s, MarketDay market)
        {
            if (s.SalesDisplay.Count == 0) return null;

            // 가중치 무작위 순서: 키 = u^(1/w) 큰 순 (가중치가 클수록 앞에 오기 쉬움)
            var order = new List<KeyValuePair<double, CardData>>(s.SalesDisplay.Count);
            foreach (string id in s.SalesDisplay)
            {
                CardData c = cards.Get(id);
                if (c == null) continue;
                double w = prices.IsFalling(c, market) ? System.Math.Max(0.01, economy.fallingLookWeight) : LookWeight(prices.PopularRank(c, market));
                double u = random.NextDouble();
                order.Add(new KeyValuePair<double, CardData>(System.Math.Pow(u, 1.0 / w), c));
            }
            order.Sort((a, b) => b.Key.CompareTo(a.Key));

            foreach (KeyValuePair<double, CardData> e in order)
                if (random.NextDouble() < BuyChance(e.Value, market)) return e.Value;
            return null;
        }

        /// <summary>둘러보다 이 카드를 살 확률 (한 장 기준).</summary>
        public float BuyChance(CardData card, MarketDay market)
        {
            float chance = rarity.Get(card.rarity).customerBuyChance * economy.browseBuyFactor;
            int rank = prices.PopularRank(card, market);
            if (rank == 1) chance *= economy.primaryBuyBoost;
            else if (rank == 2) chance *= economy.secondaryBuyBoost;
            return Mathf.Min(economy.maxBuyChance, chance);
        }

        double LookWeight(int rank)
        {
            float w = rank == 1 ? economy.primaryLookWeight : rank == 2 ? economy.secondaryLookWeight : 1f;
            return w > 0.01f ? w : 0.01f;
        }

        /// <summary>
        /// 2단계: 결제 완료. 손님 수를 세고, 진열 카드면 진열대에서 빼서 당일 장부에 판매를 기록합니다(정산 때 합산).
        /// 카드가 그새 진열대에서 사라졌으면 가게 재고를 산 것으로 보고 false.
        /// <paramref name="net"/>: 정산 때 받을 금액(수수료 뒤). 판매가 아니면 0.
        /// </summary>
        public bool CompleteSale(GameSession s, CardData card, MarketDay market, out int net)
        {
            net = 0;
            s.Ledger.customersServed++;
            int slot = card != null ? s.SalesDisplay.IndexOf(card.id) : -1;
            if (card == null || !s.SalesDisplay.Remove(card.id)) return false;
            net = prices.SellPrice(card, market);
            if (slot >= economy.salesDisplaySlots) { s.Ledger.extraSlotSales++; s.Ledger.extraSlotNet += net; } // 바인더로 늘린 칸 (가구 덕분에)
            s.Ledger.sales.Add(new DayLedger.Sale
            {
                cardId = card.id,
                gross = prices.GrossPrice(card, market),
                net = net,
            });
            return true;
        }

        /// <summary>손님이 내는 금액(수수료 전). 계산대 화면 표시용.</summary>
        public int CustomerPrice(CardData card, MarketDay market) => prices.GrossPrice(card, market);

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
