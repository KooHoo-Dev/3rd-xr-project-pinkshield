using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>카드 도둑의 무료 팩 상태 (FreePackState, 카드도둑_설계 2장) + 등장 시계 (SpawnClock).</summary>
    public class FreePackStateTests
    {
        static FreePackState Carried()
        {
            var s = new FreePackState();
            Assert.IsTrue(s.Steal());
            return s;
        }

        [Test]
        public void StartDay_PackAtHome_ThiefCanCome()
        {
            var s = new FreePackState();
            Assert.IsTrue(s.AtHome);
            Assert.IsFalse(s.OnThief);
        }

        [Test]
        public void Steal_FromHome_BecomesCarried_NoMoreThieves()
        {
            FreePackState s = Carried();
            Assert.AreEqual(FreePackPlace.Carried, s.Place);
            Assert.IsFalse(s.AtHome, "훔쳐 간 동안은 다음 도둑이 오지 않음");
            Assert.IsTrue(s.OnThief);
            Assert.IsFalse(s.Steal(), "이미 없는 팩은 또 못 훔침");
        }

        [Test]
        public void Caught_WhileCarried_DropsPack()
        {
            FreePackState s = Carried();
            Assert.IsTrue(s.Caught(), "들고 있었으면 떨어뜨림");
            Assert.AreEqual(FreePackPlace.Dropped, s.Place);
            Assert.AreEqual(FreePackState.Move.None, s.ThiefRemoved(), "떨어진 팩은 도둑과 함께 사라지지 않음");
        }

        [Test]
        public void Caught_BeforeSteal_NoDrop()
        {
            var s = new FreePackState();
            Assert.IsFalse(s.Caught(), "훔치기 전에 막음");
            Assert.IsTrue(s.AtHome);
        }

        [Test]
        public void PickUp_DroppedPack_GoesHome()
        {
            FreePackState s = Carried();
            s.Caught();
            Assert.IsTrue(s.PickUp());
            Assert.IsTrue(s.AtHome);
        }

        [Test]
        public void PickUp_PackAtHome_Ignored()
        {
            var s = new FreePackState();
            Assert.IsFalse(s.PickUp(), "제자리 팩을 잡는 건 되찾기가 아님");
        }

        [Test]
        public void Escaped_ThenRemoved_PackHiddenForTheDay()
        {
            FreePackState s = Carried();
            Assert.IsTrue(s.Escaped());
            Assert.AreEqual(FreePackPlace.Stolen, s.Place);
            Assert.AreEqual(FreePackState.Move.HomeHidden, s.ThiefRemoved(), "도둑과 함께 사라지지 않게 떼어 내되 오늘은 숨김");
            Assert.AreEqual(FreePackPlace.Gone, s.Place);
            Assert.IsFalse(s.AtHome, "잃은 날은 도둑이 다시 오지 않음");
            Assert.AreEqual(FreePackState.Move.None, s.ForceEnd());
        }

        [Test]
        public void ForceEnd_WhileCarried_NotStolen()
        {
            FreePackState s = Carried();
            Assert.AreEqual(FreePackState.Move.Home, s.ForceEnd(), "강제 종료는 도난으로 치지 않음");
            Assert.IsTrue(s.AtHome);
        }

        [Test]
        public void ForceEnd_WhileDropped_CountsAsRecovered()
        {
            FreePackState s = Carried();
            s.Caught();
            Assert.AreEqual(FreePackState.Move.Home, s.ForceEnd(), "잡고 안 주운 팩은 되찾은 것으로 침");
            Assert.IsTrue(s.AtHome);
        }

        [Test]
        public void StartDay_AfterStolen_ResetsToHome()
        {
            FreePackState s = Carried();
            s.Escaped();
            s.ThiefRemoved();
            s.StartDay();
            Assert.IsTrue(s.AtHome);
        }

        // ---------------------------------------------------------- 등장 시계

        [Test]
        public void SpawnClock_FiresOncePerScheduledTime()
        {
            var clock = new SpawnClock();
            clock.Start(new[] { 1f, 2f });
            Assert.IsFalse(clock.Tick(0.5f), "첫 시각 전");
            Assert.IsTrue(clock.Tick(0.6f), "1.1초: 첫 차례");
            Assert.IsFalse(clock.Tick(0.1f));
            Assert.IsTrue(clock.Tick(1f), "2.2초: 둘째 차례");
            Assert.IsFalse(clock.HasPending);
        }

        [Test]
        public void SpawnClock_LateTicksReleaseOneAtATime()
        {
            var clock = new SpawnClock();
            clock.Start(new[] { 1f, 2f });
            Assert.IsTrue(clock.Tick(5f));
            Assert.IsTrue(clock.Tick(0f), "밀린 차례는 다음 Tick에 하나씩");
            Assert.IsFalse(clock.Tick(0f));
        }

        [Test]
        public void SpawnClock_StopAndEmptySchedule()
        {
            var clock = new SpawnClock();
            clock.Start(new float[0]);
            Assert.IsFalse(clock.Running, "일정이 없으면 시작하지 않음");
            clock.Start(new[] { 1f });
            clock.Stop();
            Assert.IsFalse(clock.HasPending);
            Assert.IsFalse(clock.Tick(10f), "멈추면 더 안 나옴");
        }
    }
}
