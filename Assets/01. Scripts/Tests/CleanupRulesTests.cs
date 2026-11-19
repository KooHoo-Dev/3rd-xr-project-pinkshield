using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>청소 규칙 (기획서 5-1 · 설계서 7-4 · Q1).</summary>
    public class CleanupRulesTests
    {
        static readonly CampaignConfig.CleanupSpawnSettings Defaults = new CampaignConfig.CleanupSpawnSettings();

        [Test]
        public void Pick_OnlyReturnsKindsEnabledAndAvailable()
        {
            var rng = new SystemRandomSource(7);
            for (int i = 0; i < 200; i++)
            {
                CleanupKinds k = CleanupRules.Pick(CleanupKinds.Trash | CleanupKinds.MessyPack, CleanupKinds.All, Defaults, rng);
                Assert.That(k == CleanupKinds.Trash || k == CleanupKinds.MessyPack, $"켜지 않은 종류가 나옴: {k}");
            }
        }

        [Test]
        public void Pick_SkipsKindsWithNoRoom_AndReturnsNoneWhenNothingFits()
        {
            var rng = new SystemRandomSource(3);
            // 진열대 팩 꽂이가 비었으면(MessyPack 없음) · 얼룩을 아직 못 만들면 쓰레기만
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(CleanupKinds.Trash, CleanupRules.Pick(CleanupKinds.All, CleanupKinds.Trash, Defaults, rng));
            Assert.AreEqual(CleanupKinds.None, CleanupRules.Pick(CleanupKinds.MessyPack, CleanupKinds.Trash, Defaults, rng));
            Assert.AreEqual(CleanupKinds.None, CleanupRules.Pick(CleanupKinds.None, CleanupKinds.All, Defaults, rng), "청소를 끈 날은 안 생김");
            var noTrash = new CampaignConfig.CleanupSpawnSettings { trashWeight = 0 };
            Assert.AreEqual(CleanupKinds.None, CleanupRules.Pick(CleanupKinds.Trash, CleanupKinds.Trash, noTrash, rng), "가중치 0인 종류는 안 나옴");
        }

        [Test]
        public void Pick_FollowsWeights_StainTrashPack_45_35_20()
        {
            var rng = new SystemRandomSource(11);
            int trash = 0, messy = 0, stain = 0;
            const int n = 6000;
            for (int i = 0; i < n; i++)
            {
                CleanupKinds k = CleanupRules.Pick(CleanupKinds.All, CleanupKinds.All, Defaults, rng);
                if (k == CleanupKinds.Trash) trash++;
                else if (k == CleanupKinds.MessyPack) messy++;
                else if (k == CleanupKinds.FloorStain) stain++;
            }
            Assert.AreEqual(n, trash + messy + stain, "창문 얼룩(DEC-014)은 나오지 않음");
            Assert.That(stain / (float)n, Is.EqualTo(0.45f).Within(0.03f));
            Assert.That(trash / (float)n, Is.EqualTo(0.35f).Within(0.03f));
            Assert.That(messy / (float)n, Is.EqualTo(0.20f).Within(0.03f));
        }

        [Test]
        public void ShouldSpawn_FirstOfDayOne_CapAndGuarantee()
        {
            var rng = new SystemRandomSource(5);
            var never = new CampaignConfig.CleanupSpawnSettings { chance = 0f, firstDayChance = 0f, maxActive = 3, guaranteeAfter = 4 };
            Assert.IsTrue(CleanupRules.ShouldSpawn(never, 1, 0, 0, 0, rng), "1일차 첫 청소는 반드시 (튜토리얼)");
            Assert.IsFalse(CleanupRules.ShouldSpawn(never, 1, 1, 0, 0, rng), "그 뒤 1일차는 확률대로");
            Assert.IsFalse(CleanupRules.ShouldSpawn(never, 2, 0, 0, 3, rng), "3명 연속 안 생긴 건 아직 보정 전");
            Assert.IsTrue(CleanupRules.ShouldSpawn(never, 2, 0, 0, 4, rng), "4명 연속 안 생기면 반드시");

            var always = new CampaignConfig.CleanupSpawnSettings { chance = 1f, maxActive = 3 };
            Assert.IsTrue(CleanupRules.ShouldSpawn(always, 2, 5, 2, 0, rng));
            Assert.IsFalse(CleanupRules.ShouldSpawn(always, 2, 5, 3, 9, rng), "3곳이 남아 있으면 보정이 와도 안 생김");
            Assert.IsFalse(CleanupRules.ShouldSpawn(always, 1, 0, 3, 0, rng), "상한은 1일차 첫 청소보다 먼저");
        }

        [Test]
        public void ShouldSpawn_DefaultChance_About35Percent()
        {
            var rng = new SystemRandomSource(9);
            int hit = 0;
            const int n = 4000;
            for (int i = 0; i < n; i++) if (CleanupRules.ShouldSpawn(Defaults, 3, 1, 0, 0, rng)) hit++;
            Assert.That(hit / (float)n, Is.EqualTo(0.35f).Within(0.03f));
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
