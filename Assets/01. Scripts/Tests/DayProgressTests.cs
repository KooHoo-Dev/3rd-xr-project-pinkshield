using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>
    /// 일차 진행 규칙: 마지막 날 판단(CampaignConfig.IsLastDay), 하루 넘기기(GameSession.AdvanceDay),
    /// 마지막 날에만 안 팔린 진열 카드 반환(SalesRules.ReturnUnsoldOnFinalDay).
    /// 페이즈 전환 자체(GamePhases)는 씬 오브젝트가 필요해 여기서는 그 페이즈들이 쓰는 규칙을 고정합니다.
    /// </summary>
    public class DayProgressTests
    {
        CampaignConfig campaign;

        [SetUp]
        public void SetUp() => campaign = ScriptableObject.CreateInstance<CampaignConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(campaign);

        // 퇴근 버튼(AfterHours): 마지막 날이면 집을 건너뛰고 최종 정산, DayEnd도 같은 판단으로 Final / 다음 날을 고름
        [Test]
        public void IsLastDay_OnlyFromLastDayOn()
        {
            int last = campaign.DayCount;
            Assume.That(last, Is.GreaterThan(1), "기본 캠페인은 여러 날");
            Assert.IsFalse(campaign.IsLastDay(1));
            Assert.IsFalse(campaign.IsLastDay(last - 1));
            Assert.IsTrue(campaign.IsLastDay(last));
            Assert.IsTrue(campaign.IsLastDay(last + 1), "넘어가도 마지막 날로 봄 (Final로 가야 끝남)");
        }

        [Test]
        public void IsLastDay_SingleDayCampaign_FirstDayIsLast()
        {
            campaign.days.RemoveRange(1, campaign.days.Count - 1);
            Assert.AreEqual(1, campaign.DayCount);
            Assert.IsTrue(campaign.IsLastDay(1));
        }

        [Test]
        public void AdvanceDay_KeepsYesterdayLedgerAndStartsFresh()
        {
            var s = new GameSession(0);
            Assert.AreEqual(1, s.Day);
            Assert.IsNull(s.Previous, "1일차에는 어제 기록이 없음 (아침 대사도 이 값으로 건너뜀)");
            s.Ledger.cleanupsDone = 2;

            s.AdvanceDay();

            Assert.AreEqual(2, s.Day);
            Assert.AreEqual(2, s.Previous.cleanupsDone, "어제 장부 유지");
            Assert.AreEqual(0, s.Ledger.cleanupsDone, "오늘 장부는 새로");
        }

        // DayEnd: 평소에는 집(HomePhase)에서 이미 돌려놓으므로 아무것도 안 함 (집에서 다시 등록한 카드를 지우지 않게)
        [Test]
        public void ReturnUnsoldOnFinalDay_OnlyOnLastDay()
        {
            var f = new Fixture();
            try
            {
                var s = new GameSession(0);
                SalesRules sales = f.Sales(new ScriptedRandom());
                s.Collection.Add("F-SR");
                Assert.AreEqual(DisplayResult.Ok, sales.Display(s, "F-SR"));

                Assert.AreEqual(0, sales.ReturnUnsoldOnFinalDay(s, dayCount: 3), "1일차: 그대로 진열");
                Assert.AreEqual(1, s.SalesDisplay.Count);

                s.AdvanceDay();
                s.AdvanceDay();
                Assert.AreEqual(1, sales.ReturnUnsoldOnFinalDay(s, dayCount: 3), "마지막 날: 돌려놓음 (최종 점수에 카드 가치)");
                Assert.AreEqual(0, s.SalesDisplay.Count);
                Assert.AreEqual(1, s.Collection.CountOf("F-SR"), "보유 목록으로");
            }
            finally { f.Destroy(); }
        }
    }
}
