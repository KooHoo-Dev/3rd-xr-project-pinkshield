using System;
using CardShop.Core;
using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>손님 말풍선 대사 (2026-11-18 기기 테스트 피드백: 손님이 말을 안 해 단조로움).</summary>
    public class CustomerLinesTests
    {
        [Test]
        public void EveryMoment_HasANonEmptyDefaultLine()
        {
            var lines = new CustomerLines(null, new SystemRandomSource(1));
            foreach (CustomerMoment m in Enum.GetValues(typeof(CustomerMoment)))
                Assert.IsFalse(string.IsNullOrWhiteSpace(lines.Pick(m)), $"{m} 기본 대사가 비어 있음");
        }

        [Test]
        public void Parse_ReadsMomentLines_AndSkipsCommentsAndUnknown()
        {
            var parsed = CustomerLines.Parse("# 주석\n\nenter|안녕!\nBrowse | 오 이거 \nNope|무시\nPaid|\n|문장만\nPaid|고마워요");
            Assert.AreEqual(new[] { "안녕!" }, parsed[CustomerMoment.Enter].ToArray());
            Assert.AreEqual(new[] { "오 이거" }, parsed[CustomerMoment.Browse].ToArray());
            Assert.AreEqual(new[] { "고마워요" }, parsed[CustomerMoment.Paid].ToArray());
            Assert.AreEqual(3, parsed.Count);
        }

        [Test]
        public void CustomLines_ReplaceDefaults_OnlyForTheirMoment()
        {
            var lines = new CustomerLines("Enter|어서 와 나 손님", new SystemRandomSource(3));
            Assert.AreEqual("어서 와 나 손님", lines.Pick(CustomerMoment.Enter));
            Assert.IsNotEmpty(lines.Pick(CustomerMoment.Angry), "목록에 없는 상황은 기본 대사");
        }

        [Test]
        public void SameMoment_DoesNotRepeatTheLastLine()
        {
            var lines = new CustomerLines(null, new SystemRandomSource(7));
            string last = lines.Pick(CustomerMoment.Paid);
            for (int i = 0; i < 50; i++)
            {
                string next = lines.Pick(CustomerMoment.Paid);
                Assert.AreNotEqual(last, next, "같은 대사가 연달아 나옴");
                last = next;
            }
        }

        [Test]
        public void CardName_IsFilledIn()
        {
            var lines = new CustomerLines("PaidCard|{card} 득템!", new SystemRandomSource(1));
            Assert.AreEqual("불꽃 슬라임 득템!", lines.Pick(CustomerMoment.PaidCard, "불꽃 슬라임"));
            Assert.AreEqual("이 카드 득템!", lines.Pick(CustomerMoment.PaidCard), "카드 이름이 없으면 '이 카드'");
        }
    }
}
