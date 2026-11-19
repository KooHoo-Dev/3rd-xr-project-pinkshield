using System.Collections.Generic;
using CardShop.Core;
using CardShop.Tutorial;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>기본 튜토리얼 대본 검사 (튜토리얼_설계 8-5의 11 · 12, 0장 "정지 2회").</summary>
    public class TutorialDefaultsTests
    {
        static readonly Dictionary<string, string> Values = new Dictionary<string, string>
        {
            ["packPrice"] = "10", ["boxPrice"] = "90", ["cleanupBonus"] = "5", ["cleanupPenalty"] = "3", ["closingCleanupSeconds"] = "30",
        };

        [Test]
        public void Defaults_HaveNoStructureProblems()
        {
            List<string> problems = TutorialSequence.Problems(TutorialDefaults.Steps());
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void Defaults_StopTheGameAtEachFirstTimeTask()
        {
            var holdStarts = new List<string>();
            foreach (TutorialStep s in TutorialDefaults.Steps())
                if (s.kind == TutorialStepKind.Hold && s.startOn != TutorialSignal.None) holdStarts.Add(s.key); // 이어지는 정지 단계는 같은 정지로 셈
            CollectionAssert.AreEquivalent(
                new[] { TutorialDefaults.PayHoldKey, "trash-1", "closing-cleanup", "messy-1", "stain-1", "listed-1", "thief-hold" },
                holdStarts, "처음 하는 업무마다 정지 (DEC-028)");
        }

        [Test]
        public void Defaults_HoldAndWaitStepsAreOnlyInTheirPhases()
        {
            foreach (TutorialStep s in TutorialDefaults.Steps())
            {
                if (s.kind == TutorialStepKind.Hold) Assert.AreEqual(GamePhaseId.Business, s.phase, $"{s.key}: 정지는 시간이 흐르는 영업에서만");
                if (s.kind == TutorialStepKind.Wait) Assert.AreEqual(GamePhaseId.Intro, s.phase, $"{s.key}: 기다리는 안내는 Intro에서만");
            }
        }

        [Test]
        public void Defaults_LinesFitTheBubbleAndHaveNoUnfilledNumbers()
        {
            foreach (TutorialStep s in TutorialDefaults.Steps())
            {
                string text = TutorialLineFormatter.Format(s.line, Values);
                Assert.IsFalse(TutorialLineFormatter.HasUnfilled(text), $"{s.key}: 채우지 못한 자리표시 — {text}");
                Assert.IsTrue(TutorialLineFormatter.Fits(text), $"{s.key}: 말풍선(2줄 · 줄당 32자)을 넘음 — {text}");
            }
        }

        [Test]
        public void Intro_RunsToTheEnd_WithOnlyThePlayersActions()
        {
            var p = new TutorialProgress(TutorialDefaults.Steps());
            p.SetContext(GamePhaseId.Intro, 1);
            p.Notify(TutorialSignal.PhaseEntered);
            Assert.AreEqual("intro-hello", p.Active?.key);
            for (int i = 0; i < 40 && p.Active != null && p.Active.kind == TutorialStepKind.Say; i++) p.Tick(0.5f);
            Assert.AreEqual("intro-grab", p.Active?.key, "말 두 개가 끝나면 잡기를 기다림");

            p.Notify(TutorialSignal.GiftPackGrabbed); p.Tick(2f);
            p.Notify(TutorialSignal.GiftPackOpened); p.Tick(2f);
            p.Notify(TutorialSignal.RevealFinished); p.Tick(2f);
            Assert.AreEqual("intro-done", p.Active?.key);
            p.Tick(5f);
            Assert.IsFalse(p.HasPendingIn(GamePhaseId.Intro, 1), "Intro 안내가 모두 끝나야 영업으로 감");
        }

        [Test]
        public void FirstCheckout_HoldsUntilPaid_AndSkipsStepsAlreadyDone()
        {
            var p = new TutorialProgress(TutorialDefaults.Steps());
            p.SetContext(GamePhaseId.Business, 1);
            p.Notify(TutorialSignal.CheckoutStarted);
            Assert.AreEqual(TutorialDefaults.PayHoldKey, p.Active?.key);
            Assert.IsTrue(p.NeedsHold);

            p.Notify(TutorialSignal.ScannerGrabbed); // 말이 끝나기 전에 먼저 잡고 찍어 버림
            p.Notify(TutorialSignal.AllScanned);
            p.Tick(4f);
            Assert.AreEqual("pay-bag", p.Active?.key, "이미 찍었으면 찍기 안내는 건너뜀");
            p.Notify(TutorialSignal.AllBagged); p.Tick(2f);
            Assert.AreEqual("pay-pay", p.Active?.key);
            Assert.IsTrue(p.NeedsHold);
            p.Notify(TutorialSignal.CheckoutPaid); p.Tick(2f);
            Assert.AreEqual("pay-done", p.Active?.key);
            Assert.IsFalse(p.NeedsHold, "결제하면 정지가 풀림");
        }
    }
}
