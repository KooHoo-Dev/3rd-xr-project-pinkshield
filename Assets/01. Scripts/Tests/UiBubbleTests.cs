using CardShop.UI;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>말풍선 개편 (UI_개편_설계 9번 판): 점선 테두리 무늬 · 한 글자씩 보이기 · 말하는 사람 모양</summary>
    public class UiBubbleTests
    {
        const int B = UiSprites.Radius;
        const int Size = UiSprites.Radius * 2 + UiSprites.DashPeriod;

        [Test]
        public void DashMask_Corners_AreSolid()
        {
            Assert.AreEqual(1f, UiSprites.DashMask(0, 0, Size, Size, B));
            Assert.AreEqual(1f, UiSprites.DashMask(Size - 1, Size - 1, Size, Size, B));
        }

        [Test]
        public void DashMask_StraightEdges_AlternateEveryHalfPeriod()
        {
            int half = UiSprites.DashPeriod / 2;
            // 아래 변 (가로로 점선)
            Assert.AreEqual(1f, UiSprites.DashMask(B, 0, Size, Size, B));
            Assert.AreEqual(1f, UiSprites.DashMask(B + half - 1, 0, Size, Size, B));
            Assert.AreEqual(0f, UiSprites.DashMask(B + half, 0, Size, Size, B));
            // 왼쪽 변 (세로로 점선)
            Assert.AreEqual(1f, UiSprites.DashMask(0, B, Size, Size, B));
            Assert.AreEqual(0f, UiSprites.DashMask(0, B + half, Size, Size, B));
        }

        [Test]
        public void DashMask_OneTileHoldsExactlyOneDash()
        {
            // 가운데 한 칸만 반복(Tiled)하므로 그 칸에 선 하나 + 빈칸 하나가 꼭 맞아야 이음매가 없음
            int on = 0;
            for (int x = B; x < Size - B; x++) on += (int)UiSprites.DashMask(x, 0, Size, Size, B);
            Assert.AreEqual(UiSprites.DashPeriod / 2, on);
        }

        [TestCase(0f, 10, 0)]
        [TestCase(2.9f, 10, 2)]
        [TestCase(15f, 10, 10)]
        [TestCase(-1f, 10, 0)]
        public void Typewriter_VisibleCount_FloorsAndClamps(float shown, int total, int expected)
        {
            Assert.AreEqual(expected, TypewriterText.VisibleCount(shown, total));
        }

        [Test]
        public void BubbleStyles_BossSolid_SelfDashed_LabelsDiffer()
        {
            Assert.IsFalse(BubbleStyle.Boss.Dashed);
            Assert.IsTrue(BubbleStyle.Self.Dashed);
            Assert.AreNotEqual(BubbleStyle.Boss.Label, BubbleStyle.Self.Label);
            Assert.AreNotEqual(BubbleStyle.Boss.Line, BubbleStyle.Self.Line);
            Assert.AreNotEqual(BubbleStyle.Boss.Label, BubbleStyle.Customer.Label);
        }
    }
}
