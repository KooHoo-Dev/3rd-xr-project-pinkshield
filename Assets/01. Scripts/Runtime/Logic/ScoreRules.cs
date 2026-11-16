using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>최종 정산 화면에 보여 줄 값 (기획서 5-8).</summary>
    public sealed class FinalResult
    {
        public int Score;
        public int Money;
        public int CardValue;        // 보유 카드 전체 장수 × 기준가 (인기 속성 배율 없음)
        public int CollectedKinds;   // 도감 수집 종류 수
        public int CollectionPoints; // 수집 종류 수 × 1종당 점수
        public int TotalKinds;
        public int RareCollected;    // 수퍼레어 이상 수집 종류
        public int RareTotal;
        public ScoreConfig.Rank Rank;
        public CardData BestCard;
        public int PacksOpened;
    }

    /// <summary>최종 점수 = 보유 재화 + 보유 카드 가치 + 도감 수집 종류 수 × 10 (기획서 5-8).</summary>
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

            foreach (var pair in s.Collection.Owned)
            {
                CardData card = cards.Get(pair.Key);
                if (card != null) r.CardValue += pair.Value * rarity.BasePrice(card.rarity);
            }

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
            r.Score = r.Money + r.CardValue + r.CollectionPoints;
            r.Rank = score.GetRank(r.Score);
            return r;
        }
    }
}
