using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>손님이 살 수 있는 가게 재고 한 종류 (ShopItemConfig의 한 줄을 로직용으로 옮긴 것).</summary>
    public readonly struct StockOption
    {
        public readonly int Index;      // ShopItemConfig.items 안의 위치 (프리팹을 찾을 때 씀)
        public readonly string Name;
        public readonly int Price;
        public readonly int Weight;
        public readonly int FirstDay;   // 이 일차부터 팔림

        public StockOption(int index, string name, int price, int weight, int firstDay)
        {
            Index = index;
            Name = name;
            Price = price;
            Weight = weight;
            FirstDay = firstDay;
        }
    }

    /// <summary>장바구니의 물건 한 개. 판매 등록 카드면 Card가 있고, 가게 재고면 StockIndex가 0 이상.</summary>
    public sealed class BasketLine
    {
        public readonly string Name;
        public readonly int Price;
        public readonly int StockIndex;
        public readonly CardData Card;
        public bool Scanned { get; internal set; }

        public BasketLine(string name, int price, int stockIndex, CardData card)
        {
            Name = name;
            Price = price;
            StockIndex = stockIndex;
            Card = card;
        }
    }

    /// <summary>
    /// 계산대에 올라온 손님 한 명의 장바구니 (계산시스템_확장_설계 5-3). 찍은 개수 · 합계 · 다 찍었는지 판정.
    /// </summary>
    public sealed class Basket
    {
        readonly List<BasketLine> lines;

        public Basket(List<BasketLine> lines) => this.lines = lines;

        public IReadOnlyList<BasketLine> Lines => lines;
        public int Count => lines.Count;

        public int Total
        {
            get { int t = 0; foreach (BasketLine l in lines) t += l.Price; return t; }
        }

        public int ScannedTotal
        {
            get { int t = 0; foreach (BasketLine l in lines) if (l.Scanned) t += l.Price; return t; }
        }

        public int ScannedCount
        {
            get { int n = 0; foreach (BasketLine l in lines) if (l.Scanned) n++; return n; }
        }

        /// <summary>물건이 없으면(재고 설정이 비었을 때) 바로 true — 예전처럼 물건 없이도 결제는 됨.</summary>
        public bool AllScanned => ScannedCount == lines.Count;

        /// <summary>판매 등록 카드 (없으면 null). 손님당 최대 1장.</summary>
        public CardData Card
        {
            get { foreach (BasketLine l in lines) if (l.Card != null) return l.Card; return null; }
        }

        /// <summary>처음 찍혔을 때만 true.</summary>
        public bool MarkScanned(int index)
        {
            if (index < 0 || index >= lines.Count || lines[index].Scanned) return false;
            lines[index].Scanned = true;
            return true;
        }
    }

    /// <summary>
    /// 장바구니 뽑기 (계산시스템_확장_설계 5-3). 개수는 가중치로 뽑되 그날 최대 개수를 넘지 않고,
    /// 판매 등록 카드(있으면 1장)가 먼저 한 칸을 차지한 뒤 나머지를 가게 재고로 채웁니다.
    /// </summary>
    public static class BasketRules
    {
        /// <summary>countWeights[i] = 물건 (i+1)개의 가중치. maxCount를 넘는 칸은 무시. 최소 1.</summary>
        public static int RollCount(IReadOnlyList<int> countWeights, int maxCount, IRandomSource random)
        {
            if (countWeights == null || countWeights.Count == 0 || maxCount <= 1) return 1;
            int usable = countWeights.Count < maxCount ? countWeights.Count : maxCount;
            int total = 0;
            for (int i = 0; i < usable; i++) total += countWeights[i] > 0 ? countWeights[i] : 0;
            if (total <= 0) return 1;

            int roll = random.Range(0, total);
            for (int i = 0; i < usable; i++)
            {
                int w = countWeights[i] > 0 ? countWeights[i] : 0;
                if (roll < w) return i + 1;
                roll -= w;
            }
            return usable;
        }

        /// <summary>그날 팔리는 재고 중 가중치로 하나. 없으면 -1 (options 안의 위치).</summary>
        public static int PickStock(IReadOnlyList<StockOption> options, int day, IRandomSource random)
        {
            int total = 0;
            foreach (StockOption o in options) if (Available(o, day)) total += o.Weight;
            if (total <= 0) return -1;

            int roll = random.Range(0, total);
            for (int i = 0; i < options.Count; i++)
            {
                if (!Available(options[i], day)) continue;
                if (roll < options[i].Weight) return i;
                roll -= options[i].Weight;
            }
            return -1;
        }

        static bool Available(StockOption o, int day) => o.Weight > 0 && day >= o.FirstDay;

        /// <param name="card">판매 등록 카드를 사기로 했으면 그 카드 (SalesRules.PickPurchase), 아니면 null</param>
        /// <param name="cardPrice">손님이 내는 카드 값 (SalesRules.CustomerPrice)</param>
        public static Basket Build(CardData card, int cardPrice, int count, IReadOnlyList<StockOption> options, int day, IRandomSource random)
        {
            var lines = new List<BasketLine>();
            if (card != null) lines.Add(new BasketLine(card.displayName, cardPrice, -1, card));
            while (lines.Count < count)
            {
                int pick = PickStock(options, day, random);
                if (pick < 0) break;
                StockOption o = options[pick];
                lines.Add(new BasketLine(o.Name, o.Price, o.Index, null));
            }
            return new Basket(lines);
        }
    }
}
