using System.Collections.Generic;
using CardShop.Core;
using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    public class MorningLinesTests
    {
        static DayLedger Ledger(int best, int packs = 1, bool god = false, bool legendaryPack = false) =>
            new DayLedger { bestRarity = best, packsOpened = packs, godPack = god, legendaryPack = legendaryPack };

        [Test]
        public void Mood_FollowsYesterdaysBestResult()
        {
            Assert.AreEqual(MorningMood.Plain, MorningLines.MoodFor(2, 7, null));
            Assert.AreEqual(MorningMood.NoPacks, MorningLines.MoodFor(2, 7, Ledger(-1, 0)));
            Assert.AreEqual(MorningMood.Plain, MorningLines.MoodFor(2, 7, Ledger(1)));
            Assert.AreEqual(MorningMood.SuperRare, MorningLines.MoodFor(2, 7, Ledger(2)));
            Assert.AreEqual(MorningMood.Unique, MorningLines.MoodFor(2, 7, Ledger(3)));
            Assert.AreEqual(MorningMood.Legendary, MorningLines.MoodFor(2, 7, Ledger(4)));
            Assert.AreEqual(MorningMood.Legendary, MorningLines.MoodFor(2, 7, Ledger(3, god: true)), "갓팩은 레전더리 기분");
        }

        [Test]
        public void LastDay_OverridesYesterdaysMood()
        {
            Assert.AreEqual(MorningMood.LastDay, MorningLines.MoodFor(7, 7, Ledger(4)));
        }

        [Test]
        public void SameMoodOnConsecutiveDays_GivesDifferentLines()
        {
            for (int day = 2; day < 7; day++)
                Assert.AreNotEqual(MorningLines.For(day, 8, null), MorningLines.For(day + 1, 8, null), $"{day}→{day + 1}일차 대사가 같음");
        }

        [Test]
        public void AlwaysReturnsNonEmptyLine_AndReplacesDay()
        {
            for (int day = 0; day <= 20; day++)
            {
                string line = MorningLines.For(day, 7, Ledger(2));
                Assert.IsNotEmpty(line);
                Assert.IsFalse(line.Contains("{day}"));
            }
        }

        [Test]
        public void Parse_ReadsMoodPipeText_AndSkipsBadLines()
        {
            const string raw = "# 주석\nLegendary|어제는 꿈이었나\n\nlegendary|두 번째 {day}\nUnknown|무시\nPlain|\n파이프없음\n";
            Dictionary<MorningMood, List<string>> parsed = MorningLines.Parse(raw);
            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual(2, parsed[MorningMood.Legendary].Count);
        }

        [Test]
        public void CustomLines_WinOverDefaults_ButEmptyMoodFallsBack()
        {
            var custom = MorningLines.Parse("Unique|내 문장");
            Assert.AreEqual("내 문장", MorningLines.For(3, 7, Ledger(3), custom));
            Assert.IsNotEmpty(MorningLines.For(3, 7, Ledger(2), custom), "SuperRare는 기본 문장");
        }

        [Test]
        public void Endless_HasNoLastDay_AndRareResultsStillWin()
        {
            Assert.AreEqual(MorningMood.Endless, MorningLines.MoodFor(8, 7, Ledger(1), endless: true), "연장 날은 마지막 날이 아님");
            Assert.AreEqual(MorningMood.Endless, MorningLines.MoodFor(9, 7, Ledger(-1, 0), endless: true), "팩 안 연 날도 연장 기분");
            Assert.AreEqual(MorningMood.Endless, MorningLines.MoodFor(8, 7, null, endless: true));
            Assert.AreEqual(MorningMood.Unique, MorningLines.MoodFor(8, 7, Ledger(3), endless: true), "수퍼레어 이상은 그 기분이 우선");
            Assert.AreEqual(MorningMood.Legendary, MorningLines.MoodFor(8, 7, Ledger(1, god: true), endless: true));
        }

        [Test]
        public void Endless_DefaultLinesHaveNoDayNumber_AndRotate()
        {
            string a = MorningLines.For(8, 7, null, null, endless: true);
            string b = MorningLines.For(9, 7, null, null, endless: true);
            Assert.IsNotEmpty(a);
            Assert.AreNotEqual(a, b, "연속 연장 날 대사가 같음");
            StringAssert.DoesNotContain("{day}", a);
        }
    }
}
