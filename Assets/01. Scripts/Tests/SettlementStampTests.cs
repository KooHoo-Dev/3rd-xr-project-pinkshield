using CardShop.Logic;
using CardShop.UI;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>UI 개편 C안: 정산 도장 기준 · 재질 스프라이트 계산 · 종이 글자 색 바꾸기</summary>
    public class SettlementStampTests
    {
        [Test]
        public void PerfectDay_GetsGreat()
        {
            Assert.AreEqual(SettlementStamp.Great, SettlementStamp.For(new DayLedger { cleanupsDone = 3 }));
            Assert.AreEqual(SettlementStamp.Great, SettlementStamp.For(null));
        }

        [Test]
        public void FewMisses_GetsGood_ManyMisses_GetsWeak()
        {
            Assert.AreEqual(SettlementStamp.Good, SettlementStamp.For(new DayLedger { cleanupsMissed = 1, customersLost = 1 }));
            Assert.AreEqual(SettlementStamp.Weak, SettlementStamp.For(new DayLedger { cleanupsMissed = 2, freePackStolen = true }));
        }

        [Test]
        public void RoundedCoverage_FullInside_EmptyAtCorner()
        {
            // 64 × 64, 반지름 16: 가운데는 꽉 참, 맨 구석 픽셀은 비어 있음, 변 가운데는 꽉 참
            Assert.AreEqual(1f, UiSprites.RoundedCoverage(32, 32, 64, 64, 16), 1e-4);
            Assert.AreEqual(0f, UiSprites.RoundedCoverage(0, 0, 64, 64, 16), 1e-4);
            Assert.AreEqual(1f, UiSprites.RoundedCoverage(0, 32, 64, 64, 16), 1e-4);
        }

        [Test]
        public void Frame_IsHollowInside()
        {
            // 테두리 = 바깥 덮임 × (1 − 안쪽 덮임): 가운데는 0, 변 가장자리는 1
            float center = UiSprites.RoundedCoverage(32, 32, 64, 64, 16) * (1f - UiSprites.InsetCoverage(32, 32, 64, 64, 16, 4));
            float edge = UiSprites.RoundedCoverage(1, 32, 64, 64, 16) * (1f - UiSprites.InsetCoverage(1, 32, 64, 64, 16, 4));
            Assert.AreEqual(0f, center, 1e-4);
            Assert.AreEqual(1f, edge, 1e-4);
        }

        [Test]
        public void TearEdge_PointsDown()
        {
            // 맨 윗줄은 가로 전체가 참, 맨 아랫줄은 가운데만
            Assert.Greater(UiSprites.TearCoverage(0, 7, 16, 8), 0.4f);
            Assert.AreEqual(0f, UiSprites.TearCoverage(0, 0, 16, 8), 1e-4);
            Assert.Greater(UiSprites.TearCoverage(7, 0, 16, 8), 0.4f);
        }

        [Test]
        public void PaperColor_MapsThemeColorsToInk_KeepsOthers()
        {
            Assert.AreEqual(UiTheme.Ink, UiSkin.PaperColorFor(UiTheme.Gold));
            Assert.AreEqual(UiTheme.Ink, UiSkin.PaperColorFor(Color.white));
            Assert.AreEqual(UiTheme.InkSoft, UiSkin.PaperColorFor(UiTheme.SubText));
            Assert.AreEqual(Color.red, UiSkin.PaperColorFor(Color.red));
            // 색 강조는 지키되 종이 위에서는 같은 계열의 진한 색
            Assert.AreEqual(UiTheme.MoneyPlus, UiSkin.PaperColorFor(UiSkin.BrightGreen));
            Assert.AreEqual(UiTheme.Stamp, UiSkin.PaperColorFor(UiTheme.Accent));
        }
    }
}
