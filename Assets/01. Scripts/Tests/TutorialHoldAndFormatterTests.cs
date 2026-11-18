using System.Collections.Generic;
using CardShop.Core;
using CardShop.Tutorial;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>튜토리얼 정지 · 대사 다듬기 · 시퀀스 검사 (튜토리얼_설계 8-5).</summary>
    public class TutorialHoldAndFormatterTests
    {
        [Test]
        public void Hold_TwoReasons_BothMustBeRemoved()
        {
            var hold = new TutorialHold();
            object a = new object(), b = new object();
            hold.Add(a);
            hold.Add(b);
            hold.Remove(a);
            Assert.IsTrue(hold.IsHeld, "이유가 하나 남아 있으면 계속 정지");
            hold.Remove(b);
            Assert.IsFalse(hold.IsHeld);

            hold.Add(null);
            Assert.IsFalse(hold.IsHeld, "null 이유는 무시");
        }

        [Test]
        public void Hold_ChangedFiresOnlyOnFlip()
        {
            var hold = new TutorialHold();
            var changes = new List<bool>();
            hold.Changed += v => changes.Add(v);
            object a = new object(), b = new object();

            hold.Add(a);
            hold.Add(b);
            hold.Add(a);
            hold.Remove(a);
            hold.Remove(b);
            hold.Remove(b);
            hold.Add(a);
            hold.Clear();
            hold.Clear();

            CollectionAssert.AreEqual(new[] { true, false, true, false }, changes);
        }

        [Test]
        public void Formatter_FillsTokens_AndConvertsBackslashN()
        {
            var values = new Dictionary<string, string> { { "packPrice", "1,000" } };
            string s = TutorialLineFormatter.Format("한 팩에 {packPrice}원!\\n싸지?", values);
            Assert.AreEqual("한 팩에 1,000원!\n싸지?", s);
            Assert.IsFalse(TutorialLineFormatter.HasUnfilled(s));

            string left = TutorialLineFormatter.Format("상자는 {boxPrice}원", values);
            Assert.AreEqual("상자는 {boxPrice}원", left, "모르는 자리표시는 그대로 둠");
            Assert.IsTrue(TutorialLineFormatter.HasUnfilled(left));

            Assert.AreEqual(string.Empty, TutorialLineFormatter.Format(null, values));
            Assert.AreEqual("{x}", TutorialLineFormatter.Format("{x}", null));
        }

        [Test]
        public void Formatter_Fits_ChecksLinesAndLength()
        {
            Assert.IsTrue(TutorialLineFormatter.Fits("첫 줄\n둘째 줄"));
            Assert.IsFalse(TutorialLineFormatter.Fits("하나\n둘\n셋"), "3줄은 넘침");
            Assert.IsFalse(TutorialLineFormatter.Fits(new string('가', 33)), "줄당 32자 넘침");
            Assert.IsTrue(TutorialLineFormatter.Fits(new string('가', 32)));
            Assert.IsTrue(TutorialLineFormatter.Fits(new string('가', 10), 1, 10));
        }

        static TutorialStep Step(string key, string after = null, TutorialStepKind kind = TutorialStepKind.Say)
            => new TutorialStep { key = key, after = after, phase = GamePhaseId.Intro, kind = kind };

        [Test]
        public void Sequence_Problems_DetectsDuplicateAndBadAfter()
        {
            Assert.IsEmpty(TutorialSequence.Problems(new[] { Step("A"), Step("B", "A") }), "정상 목록은 문제 없음");

            List<string> dup = TutorialSequence.Problems(new[] { Step("A"), Step("A") });
            Assert.AreEqual(1, dup.Count, "key 겹침");

            List<string> missing = TutorialSequence.Problems(new[] { Step("A", "없음") });
            Assert.AreEqual(1, missing.Count, "없는 after");

            List<string> later = TutorialSequence.Problems(new[] { Step("A", "B"), Step("B") });
            Assert.AreEqual(1, later.Count, "뒤쪽을 가리키는 after");

            List<string> empty = TutorialSequence.Problems(new[] { Step("") });
            Assert.AreEqual(1, empty.Count, "빈 key");
        }

        [Test]
        public void Sequence_HoldCount()
        {
            var steps = new[] { Step("A", kind: TutorialStepKind.Hold), Step("B"), Step("C", kind: TutorialStepKind.Hold), null };
            Assert.AreEqual(2, TutorialSequence.HoldCount(steps));
        }
    }
}
