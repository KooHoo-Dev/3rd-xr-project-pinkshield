using CardShop.Logic;
using CardShop.Presentation;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>방 진열장 규칙 (집_업그레이드_설계 2장 E1).</summary>
    public class ShowcaseRulesTests
    {
        Fixture f;
        GameSession s;

        [SetUp]
        public void SetUp()
        {
            f = new Fixture();
            s = new GameSession(0);
        }

        [TearDown]
        public void TearDown() => f.Destroy();

        [Test]
        public void TryAdd_MovesCardFromCollectionToShowcase_DiscoveryKept()
        {
            s.Collection.Add("F-SR");
            Assert.AreEqual(ShowcaseResult.Ok, ShowcaseRules.TryAdd(s, "F-SR", 3));
            Assert.AreEqual(0, s.Collection.CountOf("F-SR"));
            Assert.AreEqual(1, ShowcaseRules.CountOf(s, "F-SR"));
            Assert.IsTrue(s.Collection.IsDiscovered("F-SR"));
        }

        [Test]
        public void TryAdd_NotOwned_Rejected()
        {
            Assert.AreEqual(ShowcaseResult.NotOwned, ShowcaseRules.TryAdd(s, "F-SR", 3));
            Assert.AreEqual(0, s.Showcase.Count);
        }

        [Test]
        public void TryAdd_Full_RejectedAndCardStaysOwned()
        {
            for (int i = 0; i < 4; i++) s.Collection.Add("F-N1");
            for (int i = 0; i < 3; i++) Assert.AreEqual(ShowcaseResult.Ok, ShowcaseRules.TryAdd(s, "F-N1", 3));
            Assert.AreEqual(ShowcaseResult.Full, ShowcaseRules.TryAdd(s, "F-N1", 3));
            Assert.AreEqual(1, s.Collection.CountOf("F-N1"));
        }

        [Test]
        public void Remove_ReturnsCardToCollection()
        {
            s.Collection.Add("F-U");
            ShowcaseRules.TryAdd(s, "F-U", 3);
            Assert.IsTrue(ShowcaseRules.Remove(s, "F-U"));
            Assert.AreEqual(1, s.Collection.CountOf("F-U"));
            Assert.AreEqual(0, s.Showcase.Count);
            Assert.IsFalse(ShowcaseRules.Remove(s, "F-U"));
        }

        [Test]
        public void ShowcasedCard_CannotBeSoldInstantly()
        {
            s.Collection.Add("F-SR");
            ShowcaseRules.TryAdd(s, "F-SR", 3);
            Assert.IsFalse(f.Sales(new ScriptedRandom()).TrySellInstantly(s, "F-SR", out _));
            Assert.AreEqual(0, s.Wallet.Money);
        }

        [Test]
        public void Score_CountsShowcaseCardOnce_NoExtraPoints()
        {
            s.Collection.Add("F-U"); // 기준가 100
            var rules = new ScoreRules(f.Score, f.Rarity, f.Db);
            FinalResult before = rules.Calculate(s);
            ShowcaseRules.TryAdd(s, "F-U", 3);
            FinalResult after = rules.Calculate(s);
            Assert.AreEqual(before.CardValue, after.CardValue);
            Assert.AreEqual(before.Score, after.Score);
            Assert.AreEqual(1, after.ShowcaseCards.Count);
        }

        [Test]
        public void MoveTo_ReordersSlots_AndClampsToEnd()
        {
            s.Showcase.AddRange(new[] { "A", "B", "C" });
            Assert.IsTrue(ShowcaseRules.MoveTo(s, 0, 2));
            CollectionAssert.AreEqual(new[] { "B", "C", "A" }, s.Showcase);
            Assert.IsTrue(ShowcaseRules.MoveTo(s, 2, 9), "빈 칸 번호면 맨 뒤");
            CollectionAssert.AreEqual(new[] { "B", "C", "A" }, s.Showcase);
            Assert.IsFalse(ShowcaseRules.MoveTo(s, 5, 0));
        }

        [TestCase(2, 0, 0, "x2")]
        [TestCase(1, 0, 1, "x1 · 진열 1")]
        [TestCase(1, 1, 2, "x1 · 등록 1 · 진열 2")]
        [TestCase(0, 0, 1, "진열 중")]
        [TestCase(0, 1, 1, "진열 중 · 등록 1")]
        [TestCase(0, 1, 0, "판매 등록 완료")]
        [TestCase(0, 0, 0, "판매됨")]
        public void BinderCountLabel_ShowsShowcase(int count, int listed, int showcased, string expected) =>
            Assert.AreEqual(expected, BinderSlotView.CountLabel(count, listed, showcased));
    }
}
