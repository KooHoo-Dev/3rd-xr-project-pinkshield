using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>카드 커뮤니티 내용 계산 (인기 속성 예보 · 인기 카드 TOP3 · 분위기 글).</summary>
    public class CommunityBoardTests
    {
        const int DayCount = 7;

        Fixture f;
        GameSession s;
        CommunityBoard board;

        [SetUp]
        public void SetUp()
        {
            f = new Fixture();
            s = new GameSession(0);
            // 인덱스: 0 불, 1 물, 2 땅 → 1~2일 불, 3~4일 물, 5~6일 불, 7일 땅
            var schedule = new PopularElementSchedule(DayCount, 2, new ScriptedRandom().Int(0, 1, 0, 2));
            board = new CommunityBoard(schedule, f.Db, new EconomyRules(f.Economy, f.Rarity));
        }

        [TearDown]
        public void TearDown() => f.Destroy();

        [Test]
        public void Forecast_ShowsTodayAndTomorrow()
        {
            CommunityReport r = board.Build(2, DayCount, s.Collection);
            Assert.AreEqual(CardElement.Fire, r.Today);
            Assert.AreEqual(CardElement.Water, r.Tomorrow);
            Assert.IsTrue(r.HasTomorrow);
        }

        [Test]
        public void Forecast_LastDayHasNoTomorrow()
        {
            CommunityReport r = board.Build(DayCount, DayCount, s.Collection);
            Assert.AreEqual(CardElement.Earth, r.Today);
            Assert.AreEqual(CardElement.None, r.Tomorrow);
            Assert.IsFalse(r.HasTomorrow);
            Assert.AreEqual(CommunityBoard.TomorrowNoneLine, r.MoodLines[1]);
        }

        [Test]
        public void Top_ExcludesCardsNotOwned()
        {
            s.Collection.Add("F-SR"); // F-U는 안 가짐
            CommunityReport r = board.Build(1, DayCount, s.Collection);
            Assert.AreEqual(1, r.TopCards.Count);
            Assert.AreEqual("F-SR", r.TopCards[0].id);
        }

        [Test]
        public void Top_ExcludesBelowSuperRare()
        {
            s.Collection.Add("F-N1"); // 불 노말 (인기 속성이지만 등급 미달)
            CommunityReport r = board.Build(1, DayCount, s.Collection);
            Assert.AreEqual(0, r.TopCards.Count);
        }

        [Test]
        public void Top_ExcludesOtherElementsAndNonSlime()
        {
            s.Collection.Add("W-R1");
            s.Collection.Add("SN-L");
            s.Collection.Add("F-SR");
            CommunityReport r = board.Build(1, DayCount, s.Collection);
            Assert.AreEqual(1, r.TopCards.Count);
            Assert.AreEqual("F-SR", r.TopCards[0].id);
        }

        [Test]
        public void Top_SortedByPriceDescending()
        {
            s.Collection.Add("F-SR");
            s.Collection.Add("F-U");
            CommunityReport r = board.Build(1, DayCount, s.Collection);
            Assert.AreEqual(2, r.TopCards.Count);
            Assert.AreEqual("F-U", r.TopCards[0].id);
            Assert.AreEqual("F-SR", r.TopCards[1].id);
        }

        [Test]
        public void Top_EmptyWhenNothingOwned_AndMoodLinesStillShown()
        {
            CommunityReport r = board.Build(1, DayCount, s.Collection);
            Assert.AreEqual(0, r.TopCards.Count);
            Assert.GreaterOrEqual(r.MoodLines.Count, 2);
            Assert.LessOrEqual(r.MoodLines.Count, 3);
            StringAssert.Contains("불", r.MoodLines[0]);
        }
    }
}
