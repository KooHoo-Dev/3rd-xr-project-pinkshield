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
    }
}
