using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>최종 정산 화면에 보여 줄 값 (기획서 5-8).</summary>
    public sealed class FinalResult
    {
        public int Score;
        public int Money;
        public int CardValue;        // 보유 카드 전체의 매입가 합 (DEC-041 Q7, 인기 속성 배율 없음). 진열장 카드 포함
        public int CollectedKinds;   // 도감 수집 종류 수
        public int CollectionPoints; // 수집 종류 수 × 1종당 점수
        public int FurnitureValue;   // 방 업그레이드에 쓴 돈 100% (DEC-031 Q2)
        public int TotalKinds;
        public int RareCollected;    // 수퍼레어 이상 수집 종류
        public int RareTotal;
        public ScoreConfig.Rank Rank;
        public CardData BestCard;
        public int PacksOpened;
        /// <summary>방 진열장에 세운 카드 (최종 판에 그림만, 따로 점수 없음 — DEC-031 Q4)</summary>
        public readonly List<CardData> ShowcaseCards = new List<CardData>();
    }

    /// <summary>최종 점수 = 보유 재화 + 보유 카드 가치 + 도감 수집 종류 수 × 10 (기획서 5-8) + 가구 값 (집_업그레이드_설계 3장).</summary>
    public sealed class ScoreRules
    {
        readonly ScoreConfig score;
        readonly RarityTable rarity;
        readonly CardDatabase cards;

        public ScoreRules(ScoreConfig score, RarityTable rarity, CardDatabase cards)
        {
            this.score = score;
            this.rarity = rarity;
            this.cards = cards;
        }

        /// <summary>진열대에 남은 카드는 먼저 보유 목록으로 돌려놓은 뒤 부르세요(SalesRules.ReturnUnsold).</summary>
        public FinalResult Calculate(GameSession s)
        {
            var r = new FinalResult
            {
                Money = s.Wallet.Money,
                TotalKinds = cards.Count,
                BestCard = s.Stats.bestCard,
                PacksOpened = s.Stats.packsOpened,
            };

            // 카드 가치 = 매입가 합 ("지금 다 팔면 얼마", DEC-041 Q7). 매입가는 소수(노멀 0.2)라 다 더한 뒤 내림
            float cardValue = 0f;
            foreach (var pair in s.Collection.Owned)
            {
                CardData card = cards.Get(pair.Key);
                if (card != null) cardValue += pair.Value * rarity.BuybackPrice(card.rarity);
            }
            // 진열장 카드는 보유 목록에서 빠져 있으므로 여기서 더함 (점수는 보유 카드와 똑같이, 이중 계산 없음)
            foreach (string id in s.Showcase)
            {
                CardData card = cards.Get(id);
                if (card == null) continue;
                cardValue += rarity.BuybackPrice(card.rarity);
                r.ShowcaseCards.Add(card);
            }
            r.CardValue = UnityEngine.Mathf.FloorToInt(cardValue + 1e-4f);

            foreach (CardData card in cards.cards)
            {
                if (card == null) continue;
                bool rare = card.rarity >= CardRarity.SuperRare;
                if (rare) r.RareTotal++;
                if (!s.Collection.IsDiscovered(card.id)) continue;
                r.CollectedKinds++;
                if (rare) r.RareCollected++;
            }

            r.CollectionPoints = r.CollectedKinds * score.pointsPerCollected;
            r.FurnitureValue = UpgradeRules.FurnitureValue(s.Upgrades);
            r.Score = r.Money + r.CardValue + r.CollectionPoints + r.FurnitureValue;
            r.Rank = score.GetRank(r.Score);
            return r;
        }
    }
}
