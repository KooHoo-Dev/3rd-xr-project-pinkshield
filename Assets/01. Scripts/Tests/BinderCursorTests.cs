using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>바인더 보는 곳 (BinderCursor) · 반짝일 카드 (StashedCards).</summary>
    public class BinderCursorTests
    {
        readonly List<CardData> cards = new List<CardData>();

        [SetUp]
        public void SetUp()
        {
            // 불 5장 · 물 3장, 한 쪽 4칸 → 전체 2쪽, 불 탭 2쪽, 물 탭 1쪽
            for (int i = 0; i < 5; i++) cards.Add(Make($"F{i}", CardElement.Fire));
            for (int i = 0; i < 3; i++) cards.Add(Make($"W{i}", CardElement.Water));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (CardData c in cards) Object.DestroyImmediate(c);
            cards.Clear();
        }

        static CardData Make(string id, CardElement element)
        {
            var c = ScriptableObject.CreateInstance<CardData>();
            c.id = id; c.element = element; c.kind = CardKind.Slime;
            return c;
        }

        CardData Card(string id) => cards.Find(c => c.id == id);

        [Test]
        public void ShowTab_GoesToFirstPage_AndPageIsClamped()
        {
            var cur = new BinderCursor(cards, 4);
            cur.ShowPage(1);
            Assert.AreEqual(1, cur.Page);
            cur.ShowPage(9);
            Assert.AreEqual(1, cur.Page, "마지막 쪽에서 멈춤");
            cur.ShowTab(BinderTab.Water);
            Assert.AreEqual(0, cur.Page, "탭을 바꾸면 첫 쪽");
            Assert.AreEqual(1, cur.PageCount);
            Assert.AreEqual("W0", cur.IdAt(0));
        }

        [Test]
        public void ShowCard_InOtherTab_SwitchesToItsElementTab()
        {
            var cur = new BinderCursor(cards, 4);
            cur.ShowTab(BinderTab.Water);
            Assert.IsTrue(cur.ShowCard(Card("F4")));
            Assert.AreEqual(BinderTab.Fire, cur.Tab, "불 카드는 불 탭으로");
            Assert.AreEqual(1, cur.Page, "불 탭 5번째 카드는 2쪽");
            Assert.AreEqual(0, cur.SlotOf("F4"));
        }

        [Test]
        public void ShowCard_InAllTab_StaysInAllTab()
        {
            var cur = new BinderCursor(cards, 4);
            Assert.IsTrue(cur.ShowCard(Card("W2")));
            Assert.AreEqual(BinderTab.All, cur.Tab);
            Assert.AreEqual(1, cur.Page);
        }

        [Test]
        public void ShowCard_Unknown_KeepsTabAndPage()
        {
            var cur = new BinderCursor(cards, 4);
            cur.ShowTab(BinderTab.Water);
            CardData stranger = Make("X", CardElement.Fire);
            Assert.IsFalse(cur.ShowCard(stranger));
            Assert.IsFalse(cur.ShowCard(null));
            Assert.AreEqual(BinderTab.Water, cur.Tab);
            Object.DestroyImmediate(stranger);
        }

        [Test]
        public void Number_IsDexNumberRegardlessOfTab()
        {
            var cur = new BinderCursor(cards, 4);
            cur.ShowTab(BinderTab.Water);
            Assert.AreEqual(6, cur.NumberOf("W0"), "물 탭에서도 전체 도감 번호");
            Assert.AreEqual(0, cur.NumberOf("NOPE"));
        }

        [Test]
        public void StashedCards_NoDuplicates_KeepsLast()
        {
            var s = new StashedCards();
            s.Add(new[] { "A", "B", "A", null });
            s.Add(new[] { "C" });
            Assert.AreEqual(3, s.Count);
            Assert.AreEqual("C", s.Last);
            Assert.IsTrue(s.Contains("B"));
            s.Clear();
            Assert.AreEqual(0, s.Count);
            Assert.IsNull(s.Last);
        }
    }
}
