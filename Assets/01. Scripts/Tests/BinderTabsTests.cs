using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    public class BinderTabsTests
    {
        readonly List<CardData> made = new List<CardData>();

        [TearDown]
        public void TearDown()
        {
            foreach (CardData c in made) Object.DestroyImmediate(c);
            made.Clear();
        }

        CardData Card(string id, CardElement element)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.id = id;
            c.element = element;
            made.Add(c);
            return c;
        }

        [Test]
        public void Order_StartsWithAll_AndCoversEveryElement()
        {
            Assert.AreEqual(BinderTab.All, BinderTabs.Order[0]);
            foreach (CardElement e in new[] { CardElement.Fire, CardElement.Water, CardElement.Earth, CardElement.Air, CardElement.Neutral, CardElement.None })
                CollectionAssert.Contains(BinderTabs.Order, BinderTabs.ForElement(e), $"{e} 카드가 들어갈 탭이 없음");
        }

        [Test]
        public void FilterIds_KeepsDatabaseOrder_AndOnlyThatElement()
        {
            var cards = new List<CardData>
            {
                Card("F1", CardElement.Fire), Card("W1", CardElement.Water), null, Card("F2", CardElement.Fire),
                Card("FD", CardElement.None), Card("N1", CardElement.Neutral),
            };

            CollectionAssert.AreEqual(new[] { "F1", "F2" }, BinderTabs.FilterIds(cards, BinderTab.Fire));
            CollectionAssert.AreEqual(new[] { "N1" }, BinderTabs.FilterIds(cards, BinderTab.Neutral), "무속성 슬라임은 무 탭");
            CollectionAssert.AreEqual(new[] { "FD" }, BinderTabs.FilterIds(cards, BinderTab.Other), "속성 없는 카드(필드 · 알)는 기타");
            CollectionAssert.AreEqual(new[] { "F1", "W1", "F2", "FD", "N1" }, BinderTabs.FilterIds(cards, BinderTab.All), "전체는 도감 순서, null 제외");
            CollectionAssert.IsEmpty(BinderTabs.FilterIds(null, BinderTab.Fire));
        }

        [Test]
        public void ElementTab_FitsOnePage_WithNineSlots()
        {
            // 속성마다 슬라임 8장 → 바인더 9칸이면 탭 하나가 한 페이지
            var cards = new List<CardData>();
            for (int i = 0; i < 8; i++) cards.Add(Card($"F{i}", CardElement.Fire));
            for (int i = 0; i < 8; i++) cards.Add(Card($"W{i}", CardElement.Water));
            var layout = new BinderLayout(BinderTabs.FilterIds(cards, BinderTab.Water), 9);
            Assert.AreEqual(1, layout.PageCount);
            Assert.AreEqual("W0", layout.IdAt(0, 0), "물 탭 첫 칸은 첫 물 카드");
        }

        [Test]
        public void ToElement_RoundTrips()
        {
            foreach (BinderTab t in BinderTabs.Order)
            {
                if (t == BinderTab.All || t == BinderTab.Other) Assert.AreEqual(CardElement.None, BinderTabs.ToElement(t));
                else Assert.AreEqual(t, BinderTabs.ForElement(BinderTabs.ToElement(t)));
            }
            Assert.AreEqual("불", BinderTabs.Label(BinderTab.Fire));
            Assert.AreEqual("무속성", BinderTabs.Label(BinderTab.Neutral));
        }
    }
}
