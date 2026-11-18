using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>손님 인내 시간 (PatienceRules): 물건 수 추가 · 단계 · 대사 한 번씩.</summary>
    public class PatienceRulesTests
    {
        [Test]
        public void Seconds_ExtraPerItemOnlyAtCheckout()
        {
            Assert.AreEqual(30f + 2f * 4f, PatienceRules.Seconds(30f, 4f, 3, atCheckout: true), 1e-4f, "물건 3개 → 추가 2번");
            Assert.AreEqual(30f, PatienceRules.Seconds(30f, 4f, 3, atCheckout: false), 1e-4f, "계산 중이 아니면 기본만");
            Assert.AreEqual(30f, PatienceRules.Seconds(30f, 4f, 0, atCheckout: true), 1e-4f, "물건 0개여도 음수 추가 없음");
        }

        [Test]
        public void Stage_FollowsRatio()
        {
            Assert.AreEqual(PatienceStage.Calm, PatienceRules.Stage(3f, 10f, 0.35f, 0.75f));
            Assert.AreEqual(PatienceStage.Waiting, PatienceRules.Stage(4f, 10f, 0.35f, 0.75f));
            Assert.AreEqual(PatienceStage.Impatient, PatienceRules.Stage(8f, 10f, 0.35f, 0.75f));
            Assert.AreEqual(PatienceStage.Expired, PatienceRules.Stage(10f, 10f, 0.35f, 0.75f));
        }

        [Test]
        public void LineToSay_EachLineOnce()
        {
            Assert.AreEqual(PatienceStage.Waiting, PatienceRules.LineToSay(PatienceStage.Waiting, false, false));
            Assert.AreEqual(PatienceStage.Calm, PatienceRules.LineToSay(PatienceStage.Waiting, true, false), "이미 말함");
            Assert.AreEqual(PatienceStage.Impatient, PatienceRules.LineToSay(PatienceStage.Impatient, true, false));
            Assert.AreEqual(PatienceStage.Calm, PatienceRules.LineToSay(PatienceStage.Impatient, true, true), "재촉도 한 번");
            Assert.AreEqual(PatienceStage.Impatient, PatienceRules.LineToSay(PatienceStage.Impatient, false, false), "한 번에 재촉까지 왔으면 재촉만");
            Assert.AreEqual(PatienceStage.Calm, PatienceRules.LineToSay(PatienceStage.Calm, false, false));
        }
    }
}
