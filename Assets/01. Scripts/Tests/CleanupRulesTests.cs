using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>청소 규칙 (기획서 5-1 · 설계서 7-4 · Q1).</summary>
    public class CleanupRulesTests
    {
        [Test]
        public void Pick_OnlyReturnsKindsEnabledAndAvailable()
        {
            var rng = new SystemRandomSource(7);
            for (int i = 0; i < 200; i++)
            {
                CleanupKinds k = CleanupRules.Pick(CleanupKinds.Trash | CleanupKinds.MessyPack, CleanupKinds.All, rng);
                Assert.That(k == CleanupKinds.Trash || k == CleanupKinds.MessyPack, $"켜지 않은 종류가 나옴: {k}");
            }
        }

        [Test]
        public void Pick_SkipsKindsWithNoRoom_AndReturnsNoneWhenNothingFits()
        {
            var rng = new SystemRandomSource(3);
            // 진열대 팩 꽂이가 비었으면(MessyPack 없음) 쓰레기만
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(CleanupKinds.Trash, CleanupRules.Pick(CleanupKinds.All, CleanupKinds.Trash, rng));
            Assert.AreEqual(CleanupKinds.None, CleanupRules.Pick(CleanupKinds.MessyPack, CleanupKinds.Trash, rng));
            Assert.AreEqual(CleanupKinds.None, CleanupRules.Pick(CleanupKinds.None, CleanupKinds.All, rng), "청소를 끈 날은 안 생김");
        }

        [Test]
        public void Pick_BothKinds_RoughlyEven()
        {
            var rng = new SystemRandomSource(11);
            int trash = 0, messy = 0;
            const int n = 3000;
            for (int i = 0; i < n; i++)
            {
                CleanupKinds k = CleanupRules.Pick(CleanupKinds.All, CleanupKinds.All, rng);
                if (k == CleanupKinds.Trash) trash++;
                else if (k == CleanupKinds.MessyPack) messy++;
            }
            Assert.AreEqual(n, trash + messy, "창문 얼룩(DEC-014)은 나오지 않음");
            Assert.That(trash / (float)n, Is.EqualTo(0.5f).Within(0.04f));
        }

        [Test]
        public void ClosingDone_WaitsForCustomers_ThenCleanupOrTimeout()
        {
            Assert.IsFalse(CleanupRules.ClosingDone(true, 0, 100f, 30f), "손님이 남았으면 정산으로 가지 않음");
            Assert.IsTrue(CleanupRules.ClosingDone(false, 0, 0f, 30f), "남은 청소가 없으면 바로 정산");
            Assert.IsFalse(CleanupRules.ClosingDone(false, 2, 29.9f, 30f), "청소가 남았으면 마감 정리 시간 동안 기다림");
            Assert.IsTrue(CleanupRules.ClosingDone(false, 2, 30f, 30f), "마감 정리 시간이 지나면 남은 청소는 감점으로 넘어감");
        }
    }
}
