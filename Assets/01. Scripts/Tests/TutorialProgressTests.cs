using System.Collections.Generic;
using CardShop.Core;
using CardShop.Tutorial;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>튜토리얼 진행 규칙 (튜토리얼_설계 7장 · 8-5).</summary>
    public class TutorialProgressTests
    {
        static TutorialStep S(string key,
            GamePhaseId phase = GamePhaseId.Intro,
            TutorialSignal startOn = TutorialSignal.PhaseEntered,
            TutorialSignal doneOn = TutorialSignal.None,
            string after = null,
            float seconds = 0f,
            float timeout = 0f,
            int fromDay = 1,
            TutorialStepKind kind = TutorialStepKind.Say)
        {
            return new TutorialStep
            {
                key = key, phase = phase, startOn = startOn, doneOn = doneOn, after = after,
                seconds = seconds, timeout = timeout, fromDay = fromDay, kind = kind,
            };
        }

        // 시작 · 끝 · 취소 기록을 모으는 도우미
        sealed class Log
        {
            public readonly List<string> started = new List<string>();
            public readonly List<string> ended = new List<string>();
            public readonly List<string> canceled = new List<string>();

            public Log(TutorialProgress p)
            {
                p.Started += s => started.Add(s.key);
                p.Ended += s => ended.Add(s.key);
                p.Canceled += s => canceled.Add(s.key);
            }
        }

        static TutorialProgress Make(out Log log, params TutorialStep[] steps)
        {
            var p = new TutorialProgress(steps);
            log = new Log(p);
            return p;
        }

        [Test]
        public void NoNextStep_WithoutDoneSignal()
        {
            var p = Make(out _,
                S("A", doneOn: TutorialSignal.GiftPackGrabbed),
                S("B", startOn: TutorialSignal.None, after: "A"));
            p.SetContext(GamePhaseId.Intro, 1);
            p.Notify(TutorialSignal.PhaseEntered);
            Assert.AreEqual("A", p.Active?.key);

            p.Tick(30f);
            p.Notify(TutorialSignal.BoxOpened);
            Assert.AreEqual("A", p.Active?.key, "완료 신호 없이 넘어가면 안 됨");

            p.Notify(TutorialSignal.GiftPackGrabbed);
            Assert.AreEqual("B", p.Active?.key);
        }

        [Test]
        public void SameDoneSignalTwice_AdvancesOnce()
        {
            var p = Make(out Log log,
                S("A", doneOn: TutorialSignal.GiftPackGrabbed),
                S("B", startOn: TutorialSignal.None, after: "A", doneOn: TutorialSignal.GiftPackOpened),
                S("C", startOn: TutorialSignal.None, after: "B"));
            p.SetContext(GamePhaseId.Intro, 1);
            p.Notify(TutorialSignal.PhaseEntered);
            p.Notify(TutorialSignal.GiftPackGrabbed);
            p.Notify(TutorialSignal.GiftPackGrabbed);

            Assert.AreEqual("B", p.Active?.key, "두 번째 신호로 B까지 넘어가면 안 됨");
            CollectionAssert.AreEqual(new[] { "A" }, log.ended);
        }

        [Test]
        public void AlreadyDoneByPlayer_SkippedSilently_AndChainContinues()
        {
            var p = Make(out Log log,
                S("A", seconds: 1f),
                S("B", startOn: TutorialSignal.None, after: "A", doneOn: TutorialSignal.GiftPackGrabbed),
                S("C", startOn: TutorialSignal.None, after: "B", doneOn: TutorialSignal.GiftPackOpened));
            p.SetContext(GamePhaseId.Intro, 1);
            p.Notify(TutorialSignal.PhaseEntered);
            p.Notify(TutorialSignal.GiftPackGrabbed); // B가 나오기 전에 플레이어가 먼저 잡음
            p.Tick(1f);

            Assert.IsTrue(p.IsDone("B"));
            Assert.AreEqual("C", p.Active?.key);
            CollectionAssert.AreEqual(new[] { "A", "C" }, log.started, "B는 보여 주지 않음");
            CollectionAssert.DoesNotContain(log.ended, "B");
        }

        [Test]
        public void StepWaitsForStartSignal()
        {
            var p = Make(out _, S("A", GamePhaseId.Business, startOn: TutorialSignal.CheckoutStarted));
            p.SetContext(GamePhaseId.Business, 1);
            p.Notify(TutorialSignal.PhaseEntered);
            p.Tick(10f);
            Assert.IsNull(p.Active, "손님이 오기 전에는 시작하지 않음");

            p.Notify(TutorialSignal.CheckoutStarted);
            Assert.AreEqual("A", p.Active?.key);
        }

        [Test]
        public void FromDay3_StartsOnlyFromDay3()
        {
            var p = Make(out _, S("A", GamePhaseId.Business, fromDay: 3));
            for (int day = 1; day <= 2; day++)
            {
                p.SetContext(GamePhaseId.Business, day);
                p.Notify(TutorialSignal.PhaseEntered);
                Assert.IsNull(p.Active, $"{day}일차에 시작하면 안 됨");
            }
            p.SetContext(GamePhaseId.Business, 3);
            p.Notify(TutorialSignal.PhaseEntered);
            Assert.AreEqual("A", p.Active?.key);
        }

        [Test]
        public void PhaseChange_CancelsActive_AndItStartsAgainLater()
        {
            var p = Make(out Log log, S("A", GamePhaseId.Business, startOn: TutorialSignal.CheckoutStarted,
                doneOn: TutorialSignal.CheckoutPaid, kind: TutorialStepKind.Hold));
            p.SetContext(GamePhaseId.Business, 1);
            p.Notify(TutorialSignal.CheckoutStarted);
            Assert.IsTrue(p.NeedsHold);

            p.SetContext(GamePhaseId.Settlement, 1);
            Assert.IsNull(p.Active);
            Assert.IsFalse(p.NeedsHold);
            Assert.IsFalse(p.IsDone("A"), "취소는 완료가 아님");
            CollectionAssert.AreEqual(new[] { "A" }, log.canceled);
            CollectionAssert.IsEmpty(log.ended);

            p.SetContext(GamePhaseId.Business, 2);
            p.Notify(TutorialSignal.CheckoutStarted);
            Assert.AreEqual("A", p.Active?.key);
            Assert.AreEqual(2, log.started.Count);
        }

        [Test]
        public void SkipAll_EndsEverything_AndIgnoresLaterSignals()
        {
            var p = Make(out Log log,
                S("A", doneOn: TutorialSignal.GiftPackGrabbed, kind: TutorialStepKind.Hold),
                S("B", startOn: TutorialSignal.RevealFinished));
            p.SetContext(GamePhaseId.Intro, 1);
            p.Notify(TutorialSignal.PhaseEntered);
            Assert.IsTrue(p.NeedsHold);

            p.SkipAll();
            Assert.IsNull(p.Active);
            Assert.IsFalse(p.NeedsHold);
            Assert.IsTrue(p.Skipped);
            Assert.IsTrue(p.IsDone("A") && p.IsDone("B"));
            CollectionAssert.AreEqual(new[] { "A" }, log.ended);

            p.Notify(TutorialSignal.RevealFinished);
            Assert.IsNull(p.Active);
            Assert.AreEqual(1, log.started.Count);
            Assert.IsFalse(p.HasSeen(TutorialSignal.RevealFinished), "건너뛴 뒤 신호는 기록하지 않음");
        }

        [Test]
        public void Replay_ReturnsLastShown_WithoutChangingState()
        {
            var p = Make(out Log log,
                S("A", seconds: 1f),
                S("B", startOn: TutorialSignal.BoxOpened));
            p.SetContext(GamePhaseId.Intro, 1);
            p.Notify(TutorialSignal.PhaseEntered);
            p.Tick(1f);
            Assert.IsNull(p.Active);

            Assert.AreEqual("A", p.Replay()?.key);
            Assert.IsNull(p.Active);
            Assert.IsFalse(p.IsDone("B"));
            Assert.AreEqual(1, log.started.Count, "다시 보기는 Started를 부르지 않음");
            Assert.AreEqual("A", p.LastShown?.key);
        }

        [Test]
        public void MinDisplaySeconds_EarlyDoneSignal_EndsAfterSeconds()
        {
            var p = Make(out _,
                S("A", doneOn: TutorialSignal.GiftPackGrabbed, seconds: 2f),
                S("B", startOn: TutorialSignal.None, after: "A"));
            p.SetContext(GamePhaseId.Intro, 1);
            p.Notify(TutorialSignal.PhaseEntered);
            p.Notify(TutorialSignal.GiftPackGrabbed);
            Assert.AreEqual("A", p.Active?.key, "최소 표시 시간 전에는 끝나지 않음");

            p.Tick(1f);
            Assert.AreEqual("A", p.Active?.key);
            p.Tick(1.5f);
            Assert.AreEqual("B", p.Active?.key);
        }

        [Test]
        public void Timeout_EndsStep()
        {
            var p = Make(out Log log, S("A", doneOn: TutorialSignal.GiftPackGrabbed, timeout: 5f));
            p.SetContext(GamePhaseId.Intro, 1);
            p.Notify(TutorialSignal.PhaseEntered);
            p.Tick(4.9f);
            Assert.AreEqual("A", p.Active?.key);
            p.Tick(0.2f);
            Assert.IsNull(p.Active);
            Assert.IsTrue(p.IsDone("A"));
            CollectionAssert.AreEqual(new[] { "A" }, log.ended);
        }

        [Test]
        public void ReadyQueue_StartSignalDuringActive_StartsAfterActiveEnds()
        {
            var p = Make(out _,
                S("A", GamePhaseId.Business, doneOn: TutorialSignal.CheckoutPaid),
                S("B", GamePhaseId.Business, startOn: TutorialSignal.TrashSpawned, doneOn: TutorialSignal.TrashBinned));
            p.SetContext(GamePhaseId.Business, 1);
            p.Notify(TutorialSignal.PhaseEntered);
            p.Notify(TutorialSignal.TrashSpawned);
            Assert.AreEqual("A", p.Active?.key, "진행 중 단계를 끊지 않음");

            p.Notify(TutorialSignal.CheckoutPaid);
            Assert.AreEqual("B", p.Active?.key);
        }

        [Test]
        public void HasPendingIn_ByPhaseAndDay()
        {
            var p = Make(out _,
                S("A", seconds: 1f),
                S("B", GamePhaseId.Home, fromDay: 2));
            Assert.IsTrue(p.HasPendingIn(GamePhaseId.Intro, 1));
            Assert.IsFalse(p.HasPendingIn(GamePhaseId.Home, 1));
            Assert.IsTrue(p.HasPendingIn(GamePhaseId.Home, 2));

            p.SetContext(GamePhaseId.Intro, 1);
            p.Notify(TutorialSignal.PhaseEntered);
            Assert.IsTrue(p.HasPendingIn(GamePhaseId.Intro, 1), "진행 중도 아직 안 끝남");
            p.Tick(1f);
            Assert.IsFalse(p.HasPendingIn(GamePhaseId.Intro, 1));
        }

        [Test]
        public void SeenSignals_ClearedOnContextChange()
        {
            var p = Make(out Log log,
                S("B", GamePhaseId.AfterHours, doneOn: TutorialSignal.BinderBrowsed));
            p.SetContext(GamePhaseId.Intro, 1);
            p.Notify(TutorialSignal.BinderBrowsed);
            Assert.IsTrue(p.HasSeen(TutorialSignal.BinderBrowsed));

            p.SetContext(GamePhaseId.AfterHours, 1);
            Assert.IsFalse(p.HasSeen(TutorialSignal.BinderBrowsed));
            p.Notify(TutorialSignal.PhaseEntered);
            Assert.AreEqual("B", p.Active?.key, "Intro에서 받은 신호로 생략되면 안 됨");
            CollectionAssert.AreEqual(new[] { "B" }, log.started);
        }

        [Test]
        public void MissingAfterKey_NeverStarts_AndNullListIsSafe()
        {
            var p = Make(out _, S("A", after: "없음"));
            p.SetContext(GamePhaseId.Intro, 1);
            Assert.DoesNotThrow(() => p.Notify(TutorialSignal.PhaseEntered));
            Assert.IsNull(p.Active);

            var empty = new TutorialProgress(null);
            empty.SetContext(GamePhaseId.Intro, 1);
            empty.Notify(TutorialSignal.PhaseEntered);
            empty.Tick(1f);
            Assert.IsNull(empty.Active);
            Assert.IsFalse(empty.HasPendingIn(GamePhaseId.Intro, 1));
        }

        [Test]
        public void Reset_MakesEverythingPendingAgain()
        {
            var p = Make(out _, S("A", seconds: 1f), S("B", startOn: TutorialSignal.BoxOpened));
            p.SetContext(GamePhaseId.Intro, 1);
            p.Notify(TutorialSignal.PhaseEntered);
            p.SkipAll();

            p.Reset();
            Assert.IsFalse(p.Skipped);
            Assert.IsFalse(p.IsDone("A"));
            Assert.IsNull(p.LastShown);
            p.Notify(TutorialSignal.PhaseEntered);
            Assert.AreEqual("A", p.Active?.key);
        }
    }
}
