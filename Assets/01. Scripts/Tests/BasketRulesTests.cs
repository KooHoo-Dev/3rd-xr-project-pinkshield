using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>계산대 장바구니: 개수 뽑기 · 재고 뽑기 · 찍은 개수와 합계 (계산시스템_확장_설계 5-3).</summary>
    public class BasketRulesTests
    {
        static readonly List<StockOption> PackOnly = new List<StockOption> { new StockOption(0, "카드팩", 10, 1, 1) };

        [Test]
        public void RollCount_NeverExceedsDayMax()
        {
            var weights = new List<int> { 50, 30, 15, 5 };
            var random = new SystemRandomSource(7);
            for (int i = 0; i < 500; i++)
                Assert.That(BasketRules.RollCount(weights, 2, random), Is.InRange(1, 2));
        }

        [Test]
        public void RollCount_PicksByWeight()
        {
            var weights = new List<int> { 50, 30, 15, 5 };
            // 합계 100: 0~49 → 1개, 50~79 → 2개, 80~94 → 3개, 95~99 → 4개
            Assert.AreEqual(1, BasketRules.RollCount(weights, 4, new ScriptedRandom().Int(49)));
            Assert.AreEqual(2, BasketRules.RollCount(weights, 4, new ScriptedRandom().Int(50)));
            Assert.AreEqual(3, BasketRules.RollCount(weights, 4, new ScriptedRandom().Int(94)));
            Assert.AreEqual(4, BasketRules.RollCount(weights, 4, new ScriptedRandom().Int(95)));
        }

        [Test]
        public void RollCount_EmptyWeightsMeansOne()
        {
            Assert.AreEqual(1, BasketRules.RollCount(new List<int>(), 4, new ScriptedRandom()));
            Assert.AreEqual(1, BasketRules.RollCount(new List<int> { 0, 0 }, 4, new ScriptedRandom()));
        }

        [Test]
        public void PickStock_SkipsItemsBeforeFirstDay()
        {
            var options = new List<StockOption>
            {
                new StockOption(0, "카드팩", 10, 1, 1),
                new StockOption(1, "피규어", 40, 100, 3),
            };
            // 1일차에는 피규어가 안 팔리므로 가중치 합계 1 → 언제나 카드팩
            Assert.AreEqual(0, BasketRules.PickStock(options, 1, new ScriptedRandom().Int(0)));
            // 3일차: 합계 101, 1 이상이면 피규어
            Assert.AreEqual(1, BasketRules.PickStock(options, 3, new ScriptedRandom().Int(1)));
        }

        [Test]
        public void PickStock_NoOptionsReturnsMinusOne()
        {
            Assert.AreEqual(-1, BasketRules.PickStock(new List<StockOption>(), 1, new ScriptedRandom()));
        }

        [Test]
        public void Build_CardTakesOneSlotThenStockFills()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.displayName = "불꽃 슬라임";
            try
            {
                Basket b = BasketRules.Build(card, 120, 3, PackOnly, 1, new ScriptedRandom().Int(0, 0));
                Assert.AreEqual(3, b.Count);
                Assert.AreSame(card, b.Card);
                Assert.AreEqual(120 + 10 + 10, b.Total);
            }
            finally { Object.DestroyImmediate(card); }
        }

        [Test]
        public void Build_PacksCanRepeatButFiguresOnlyOncePerCustomer()
        {
            var options = new List<StockOption>
            {
                new StockOption(0, "카드팩", 10, 1, 1, 0),
                new StockOption(1, "피규어", 45, 100, 1, 1),
            };
            // 첫 칸은 피규어(굴림 1 → 피규어), 그 뒤로는 피규어가 빠져 가중치 합계 1 → 카드팩만
            Basket b = BasketRules.Build(null, 0, 4, options, 1, new ScriptedRandom().Int(1, 0, 0, 0));
            Assert.AreEqual(4, b.Count);
            int figures = 0, packs = 0;
            foreach (BasketLine l in b.Lines) { if (l.StockIndex == 1) figures++; else if (l.StockIndex == 0) packs++; }
            Assert.AreEqual(1, figures, "피규어는 손님당 1개");
            Assert.AreEqual(3, packs, "카드팩은 여러 개");
        }

        [Test]
        public void Build_StopsEarlyWhenEveryItemReachedItsLimit()
        {
            var options = new List<StockOption> { new StockOption(0, "카드팩 상자", 90, 1, 1, 1) };
            Basket b = BasketRules.Build(null, 0, 3, options, 1, new ScriptedRandom().Int(0, 0, 0));
            Assert.AreEqual(1, b.Count, "살 수 있는 품목이 없으면 개수가 덜 차도 멈춤");
        }

        [Test]
        public void Build_WithoutStockKeepsOnlyCard()
        {
            Basket b = BasketRules.Build(null, 0, 3, new List<StockOption>(), 1, new ScriptedRandom());
            Assert.AreEqual(0, b.Count);
            Assert.IsTrue(b.AllScanned, "물건이 없으면 바로 결제 가능 (예전 동작)");
        }

        [Test]
        public void Scanning_UpdatesCountAndTotalOnce()
        {
            Basket b = BasketRules.Build(null, 0, 2, PackOnly, 1, new ScriptedRandom().Int(0, 0));
            Assert.IsFalse(b.AllScanned);
            Assert.IsTrue(b.MarkScanned(0));
            Assert.IsFalse(b.MarkScanned(0), "같은 물건을 두 번 찍어도 한 번만");
            Assert.AreEqual(1, b.ScannedCount);
            Assert.AreEqual(10, b.ScannedTotal);
            Assert.IsTrue(b.MarkScanned(1));
            Assert.IsTrue(b.AllScanned);
            Assert.AreEqual(20, b.ScannedTotal);
            Assert.IsFalse(b.MarkScanned(5), "없는 번호는 무시");
        }

        [Test]
        public void Bagging_OnlyScannedItemsAndOnce()
        {
            Basket b = BasketRules.Build(null, 0, 2, PackOnly, 1, new ScriptedRandom().Int(0, 0));
            Assert.IsFalse(b.MarkBagged(0), "안 찍은 물건은 봉투가 받지 않음");
            b.MarkScanned(0);
            Assert.IsTrue(b.MarkBagged(0));
            Assert.IsFalse(b.MarkBagged(0), "같은 물건을 두 번 담아도 한 번만");
            Assert.AreEqual(1, b.BaggedCount);
            Assert.IsFalse(b.MarkBagged(5), "없는 번호는 무시");
        }

        [Test]
        public void Bagging_AllBaggedOnlyAfterEveryItemIsInBag()
        {
            Basket b = BasketRules.Build(null, 0, 2, PackOnly, 1, new ScriptedRandom().Int(0, 0));
            b.MarkScanned(0);
            b.MarkScanned(1);
            Assert.IsTrue(b.AllScanned);
            Assert.IsFalse(b.AllBagged, "다 찍어도 봉투에 담기 전에는 결제 불가 (K2: 찍기 → 봉투 → 결제)");
            b.MarkBagged(0);
            b.MarkBagged(1);
            Assert.IsTrue(b.AllBagged);
        }

        [Test]
        public void Bagging_EmptyBasketIsReadyToPay()
        {
            Basket b = BasketRules.Build(null, 0, 3, new List<StockOption>(), 1, new ScriptedRandom());
            Assert.IsTrue(b.AllBagged, "물건이 없으면 바로 결제 가능 (예전 동작)");
        }
    }
}
