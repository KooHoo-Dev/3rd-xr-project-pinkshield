using System;
using System.Collections.Generic;
using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    public class BinderLayoutTests
    {
        static List<string> Ids(int count)
        {
            var list = new List<string>();
            for (int i = 0; i < count; i++) list.Add($"C{i}");
            return list;
        }

        [Test]
        public void PageCount_RoundsUp()
        {
            Assert.AreEqual(6, new BinderLayout(Ids(50), 9).PageCount, "50장 / 9칸 = 6페이지");
            Assert.AreEqual(5, new BinderLayout(Ids(50), 10).PageCount, "50장 / 10칸 = 5페이지");
            Assert.AreEqual(1, new BinderLayout(Ids(0), 9).PageCount, "카드가 없어도 1페이지");
        }

        [Test]
        public void TryGetSlot_FollowsDatabaseOrder()
        {
            var layout = new BinderLayout(Ids(50), 9);
            AssertSlot(layout, "C0", 0, 0);
            AssertSlot(layout, "C8", 0, 8);
            AssertSlot(layout, "C9", 1, 0);
            AssertSlot(layout, "C49", 5, 4);
            Assert.IsFalse(layout.TryGetSlot("없는카드", out _, out _));
            Assert.IsFalse(layout.TryGetSlot(null, out _, out _));
        }

        [Test]
        public void IdAt_IsInverseOfTryGetSlot_AndEmptyBeyondLastCard()
        {
            var layout = new BinderLayout(Ids(50), 9);
            for (int i = 0; i < 50; i++)
            {
                Assert.IsTrue(layout.TryGetSlot($"C{i}", out int page, out int slot));
                Assert.AreEqual($"C{i}", layout.IdAt(page, slot));
            }
            Assert.IsNull(layout.IdAt(5, 5), "마지막 페이지의 남는 칸");
            Assert.IsNull(layout.IdAt(0, 9), "칸 번호 범위 밖");
            Assert.IsNull(layout.IdAt(-1, 0));
            Assert.IsNull(layout.IdAt(6, 0));
        }

        [Test]
        public void EmptyAndDuplicateIds_AreSkipped()
        {
            var layout = new BinderLayout(new[] { "A", null, "", "B", "A", "C" }, 2);
            Assert.AreEqual(3, layout.CardCount);
            Assert.AreEqual(2, layout.IndexOf("C"));
            Assert.AreEqual(0, layout.IndexOf("A"), "중복이면 처음 자리");
        }

        [Test]
        public void PageMoves_StopAtBothEnds()
        {
            var layout = new BinderLayout(Ids(50), 9);
            Assert.IsFalse(layout.HasPrevious(0));
            Assert.IsTrue(layout.HasNext(0));
            Assert.IsTrue(layout.HasPrevious(5));
            Assert.IsFalse(layout.HasNext(5));
            Assert.AreEqual(0, layout.ClampPage(-3));
            Assert.AreEqual(5, layout.ClampPage(99));
            Assert.AreEqual(2, layout.ClampPage(2));
        }

        [Test]
        public void ZeroSlotsPerPage_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BinderLayout(Ids(3), 0));
        }

        [Test]
        public void Collection_RaisesChanged_OnAddAndRemove()
        {
            var collection = new CardShop.Logic.Collection();
            var changed = new List<string>();
            collection.Changed += id => changed.Add(id);
            collection.Add("A");
            collection.Add("A");
            collection.Remove("A");
            collection.Remove("없음"); // 없는 카드는 바뀐 게 없으므로 알리지 않음
            CollectionAssert.AreEqual(new[] { "A", "A", "A" }, changed);
            Assert.IsTrue(collection.IsDiscovered("A"));
        }

        static void AssertSlot(BinderLayout layout, string id, int page, int slot)
        {
            Assert.IsTrue(layout.TryGetSlot(id, out int p, out int s), id);
            Assert.AreEqual(page, p, $"{id} 페이지");
            Assert.AreEqual(slot, s, $"{id} 칸");
        }
    }
}
