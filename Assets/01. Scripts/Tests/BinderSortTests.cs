using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>바인더 정렬 (BinderSort, 11-20 플레이 피드백 F1) · 정렬과 탭을 함께 쓰는 BinderCursor.</summary>
    public class BinderSortTests
    {
        readonly List<CardData> cards = new List<CardData>();

        [SetUp]
        public void SetUp()
        {
            // 도감순: F0(노말) F1(레전더리) W0(레어) F2(레전더리) W1(유니크) F3(수퍼레어)
            cards.Add(Make("F0", CardElement.Fire, CardRarity.Normal));
            cards.Add(Make("F1", CardElement.Fire, CardRarity.Legendary));
            cards.Add(Make("W0", CardElement.Water, CardRarity.Rare));
            cards.Add(Make("F2", CardElement.Fire, CardRarity.Legendary));
            cards.Add(Make("W1", CardElement.Water, CardRarity.Unique));
            cards.Add(Make("F3", CardElement.Fire, CardRarity.SuperRare));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (CardData c in cards) Object.DestroyImmediate(c);
            cards.Clear();
        }

        static CardData Make(string id, CardElement element, CardRarity rarity)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.id = id; c.element = element; c.rarity = rarity; c.kind = CardKind.Slime;
            return c;
        }

        [Test]
        public void Dex_KeepsDatabaseOrder()
        {
            CollectionAssert.AreEqual(new[] { "F0", "F1", "W0", "F2", "W1", "F3" },
                BinderSort.Ids(cards, BinderTab.All, BinderSortOrder.Dex));
        }

        [Test]
        public void Rarity_HighestFirst_SameRarityInDexOrder()
        {
            CollectionAssert.AreEqual(new[] { "F1", "F2", "W1", "F3", "W0", "F0" },
                BinderSort.Ids(cards, BinderTab.All, BinderSortOrder.Rarity));
        }

        [Test]
        public void Rarity_WithTab_SortsOnlyThatElement()
        {
            CollectionAssert.AreEqual(new[] { "F1", "F2", "F3", "F0" },
                BinderSort.Ids(cards, BinderTab.Fire, BinderSortOrder.Rarity));
        }

        [Test]
        public void OwnedFirst_KeepsOrderInsideEachGroup()
        {
            // 가진 카드: W0 · F2 · F0 (11-21 2차 V4)
            var owned = new HashSet<string> { "W0", "F2", "F0" };
            CollectionAssert.AreEqual(new[] { "F0", "W0", "F2", "F1", "W1", "F3" },
                BinderSort.Ids(cards, BinderTab.All, BinderSortOrder.Dex, owned.Contains));
            CollectionAssert.AreEqual(new[] { "F2", "W0", "F0", "F1", "W1", "F3" },
                BinderSort.Ids(cards, BinderTab.All, BinderSortOrder.Rarity, owned.Contains));
        }

        [Test]
        public void NullCards_AreSkipped()
        {
            var withNull = new List<CardData>(cards) { null };
            Assert.AreEqual(6, BinderSort.Ids(withNull, BinderTab.All, BinderSortOrder.Rarity).Count);
        }

        [Test]
        public void Next_Toggles_AndLabelIsCurrentOrder()
        {
            Assert.AreEqual(BinderSortOrder.Rarity, BinderSort.Next(BinderSortOrder.Dex));
            Assert.AreEqual(BinderSortOrder.Dex, BinderSort.Next(BinderSortOrder.Rarity));
            Assert.AreEqual("도감순", BinderSort.Label(BinderSortOrder.Dex));
            Assert.AreEqual("레어도순", BinderSort.Label(BinderSortOrder.Rarity));
        }

        [Test]
        public void Cursor_ShowSort_GoesToFirstPage_KeepsTab_AndDexNumber()
        {
            var cur = new BinderCursor(cards, 2);
            cur.ShowTab(BinderTab.Fire);
            cur.ShowPage(1);
            cur.ShowSort(BinderSortOrder.Rarity);
            Assert.AreEqual(0, cur.Page, "정렬을 바꾸면 1쪽");
            Assert.AreEqual(BinderTab.Fire, cur.Tab, "탭은 그대로");
            Assert.AreEqual("F1", cur.IdAt(0));
            Assert.AreEqual("F2", cur.IdAt(1));
            Assert.AreEqual(4, cur.NumberOf("F2"), "도감 번호는 정렬과 상관없음");
        }

        [Test]
        public void Cursor_ShowCard_FindsPageInSortedOrder()
        {
            var cur = new BinderCursor(cards, 2);
            cur.ShowSort(BinderSortOrder.Rarity);
            Assert.IsTrue(cur.ShowCard(cards.Find(c => c.id == "F0")));
            Assert.AreEqual(2, cur.Page, "레어도순에서 노말은 마지막 쪽");
            Assert.AreEqual(1, cur.SlotOf("F0"));
        }

        [Test]
        public void Cursor_Reset_BackToDex()
        {
            var cur = new BinderCursor(cards, 2);
            cur.ShowSort(BinderSortOrder.Rarity);
            cur.Reset();
            Assert.AreEqual(BinderSortOrder.Dex, cur.Sort);
            Assert.AreEqual("F0", cur.IdAt(0));
        }
    }
}
