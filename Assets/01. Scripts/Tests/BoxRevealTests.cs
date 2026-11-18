using System.Collections.Generic;
using System.Linq;
using CardShop.Cards;
using CardShop.Logic;
using CardShop.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>
    /// 시연 피드백 B3: 상자 일괄 개봉의 공개 대기열 · 스킵 · 5점 줄 · 하이라이트 · 3초 꾹 타이머.
    /// </summary>
    public class BoxRevealTests
    {
        const CardRarity N = CardRarity.Normal, R = CardRarity.Rare, S = CardRarity.SuperRare,
            U = CardRarity.Unique, L = CardRarity.Legendary;

        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        PackResult Pack(CardRarity[] rarities, bool[] isNew = null, SpecialPackType special = SpecialPackType.None)
        {
            var cards = new CardData[rarities.Length];
            for (int i = 0; i < rarities.Length; i++)
            {
                var c = ScriptableObject.CreateInstance<CardData>();
                c.id = $"T-{rarities[i]}-{i}";
                c.rarity = rarities[i];
                created.Add(c);
                cards[i] = c;
            }
            return new PackResult
            {
                Cards = cards,
                IsNew = isNew ?? new bool[rarities.Length],
                Special = special,
                IsBoxPack = true,
            };
        }

        PackResult Plain() => Pack(new[] { N, N, R, R, N });

        // ---------------------------------------------------------- 대기열

        [Test]
        public void BuildAll_OneItemPerCard_InPackAndCardOrder()
        {
            var packs = new List<PackResult> { Plain(), Plain() };
            List<RevealItem> q = RevealQueue.BuildAll(packs);
            Assert.AreEqual(10, q.Count, "한 장씩: 카드 수만큼");
            Assert.AreEqual(new RevealItem(0, 0), q[0]);
            Assert.AreEqual(new RevealItem(0, 4), q[4]);
            Assert.AreEqual(new RevealItem(1, 0), q[5]);
        }

        [Test]
        public void BuildAll_GodPackIsFiveSingleItems_LikeNormalPacks()
        {
            var packs = new List<PackResult>
            {
                Plain(),
                Pack(new[] { U, U, L, U, U }, special: SpecialPackType.GodPack),
            };
            List<RevealItem> q = RevealQueue.BuildAll(packs);
            Assert.AreEqual(10, q.Count, "갓팩도 일반 팩처럼 한 장씩 (DEC-004)");
            Assert.AreEqual(new RevealItem(1, 0), q[5]);
        }

        [Test]
        public void GodOnly_KeepsOnlyGodPackItems_InOrder()
        {
            var packs = new List<PackResult>
            {
                Plain(),
                Pack(new[] { U, U, U, U, L }, special: SpecialPackType.GodPack),
                Plain(),
                Pack(new[] { U, U, U, L, U }, special: SpecialPackType.GodPack),
            };
            List<RevealItem> q = RevealQueue.BuildAll(packs);
            List<RevealItem> kept = RevealQueue.GodOnly(packs, q, 3); // 첫 팩 3장을 넘긴 뒤 건너뜀
            Assert.AreEqual(10, kept.Count, "갓팩 2개 × 5장");
            Assert.AreEqual(new RevealItem(1, 0), kept[0]);
            Assert.AreEqual(new RevealItem(3, 4), kept[9]);
        }

        [Test]
        public void GodOnly_NoGodPack_Empty()
        {
            var packs = new List<PackResult> { Plain(), Plain() };
            Assert.IsEmpty(RevealQueue.GodOnly(packs, RevealQueue.BuildAll(packs), 0));
        }

        [Test]
        public void SinglePack_IsQueueOfLengthFive()
        {
            var packs = new List<PackResult> { Pack(new[] { N, R, N, S, U }) };
            Assert.AreEqual(5, RevealQueue.BuildAll(packs).Count, "단일 팩도 같은 진행기(길이 1)");
        }

        // ---------------------------------------------------------- 5점 줄

        [Test]
        public void DotLine_OneSymbolPerCard_WithNewCount()
        {
            PackResult p = Pack(new[] { N, R, N, S, U }, new[] { false, true, false, true, false });
            Assert.AreEqual("팩 3  ● ● ● ◆ ★  새 2", RevealQueue.DotLine(p, 3));
        }

        [Test]
        public void DotLine_NoNewCards_OmitsNewLabel()
        {
            Assert.AreEqual("팩 10  ● ● ● ● ●", RevealQueue.DotLine(Pack(new[] { N, N, R, R, N }), 10));
        }

        [Test]
        public void DotLine_Colorize_WrapsEachSymbol()
        {
            PackResult p = Pack(new[] { N, L }, new[] { false, true });
            string line = RevealQueue.DotLine(p, 1, r => r == L ? "FF0000" : null, "00FF00");
            Assert.AreEqual("팩 1  ● <color=#FF0000>★</color>  <color=#00FF00>새 1</color>", line);
        }

        // ---------------------------------------------------------- 하이라이트

        [Test]
        public void Highlights_Default_HighestRarityThenNew()
        {
            var packs = new List<PackResult>
            {
                Pack(new[] { N, R, S, N, N }, new[] { true, false, false, false, false }),
                Pack(new[] { N, N, S, N, U }, new[] { false, false, true, false, false }),
            };
            var h = RevealQueue.PickHighlights(packs, 3);
            Assert.AreEqual((1, 4), h[0], "유니크 먼저");
            Assert.AreEqual((1, 2), h[1], "같은 수퍼레어면 새 카드 먼저");
            Assert.AreEqual((0, 2), h[2]);
        }

        // ---------------------------------------------------------- 3초 꾹 타이머

        [Test]
        public void HoldTimer_FiresOnceAfterHoldSeconds()
        {
            var t = new HoldToSkipTimer(3f, 0.25f);
            Assert.IsFalse(t.Update(true, 10f), "누른 순간");
            Assert.IsFalse(t.Update(true, 12.9f));
            Assert.That(t.Progress(12.9f), Is.EqualTo(2.9f / 3f).Within(1e-4f));
            Assert.IsTrue(t.Update(true, 13.0f), "3초");
            Assert.IsFalse(t.Update(true, 14f), "계속 눌러도 한 번만");
            Assert.AreEqual(0f, t.Progress(14f), "발동 뒤 게이지 0");
        }

        [Test]
        public void HoldTimer_ReleaseResets()
        {
            var t = new HoldToSkipTimer(3f);
            t.Update(true, 0f);
            t.Update(true, 2.5f);
            t.Update(false, 2.6f); // 놓음
            Assert.IsFalse(t.Update(true, 3.1f), "새 눌림은 처음부터");
            Assert.IsFalse(t.Update(true, 6.0f));
            Assert.IsTrue(t.Update(true, 6.2f));
        }

        [Test]
        public void HoldTimer_TapsDoNotShowGauge()
        {
            var t = new HoldToSkipTimer(3f, 0.25f);
            t.Update(true, 0f);
            Assert.IsFalse(t.IsCharging(0.1f), "짧은 탭(넘기기)에는 게이지 X");
            Assert.IsTrue(t.IsCharging(0.3f));
        }

        [Test]
        public void HoldTimer_RequireRelease_IgnoresCurrentPress()
        {
            var t = new HoldToSkipTimer(3f);
            t.Update(true, 0f);   // 자르기 제스처로 누른 채 개봉
            t.RequireRelease();
            Assert.IsFalse(t.Update(true, 5f), "놓기 전에는 발동하지 않음");
            Assert.IsFalse(t.IsCharging(5f));
            t.Update(false, 5.1f);
            t.Update(true, 6f);
            Assert.IsTrue(t.Update(true, 9f), "새로 누르면 정상");
        }

        [Test]
        public void HoldTimer_RequireReleaseWhileNotPressed_DoesNotBlockNextPress()
        {
            var t = new HoldToSkipTimer(3f);
            t.RequireRelease();
            t.Update(false, 0f);
            t.Update(true, 1f);
            Assert.IsTrue(t.Update(true, 4f));
        }

        // ---------------------------------------------------------- 일렬 배치

        [Test]
        public void RowPose_EvenlySpacedAndCentered_FacingPlayer()
        {
            Vector3 head = new Vector3(0f, 1.6f, 0f);
            Pose first = BoxOpenEffect.RowPose(0, 10, head, Vector3.forward, 0.6f, 0.08f, 0.1f);
            Pose last = BoxOpenEffect.RowPose(9, 10, head, Vector3.forward, 0.6f, 0.08f, 0.1f);
            Assert.Less(first.position.x, 0f, "첫 팩은 왼쪽");
            Assert.AreEqual(-first.position.x, last.position.x, 1e-4f, "줄이 가운데 정렬");
            Assert.AreEqual(9 * 0.08f, last.position.x - first.position.x, 1e-4f, "일정한 간격");
            Assert.AreEqual(first.position.y, last.position.y, 1e-4f, "일렬 = 같은 높이");
            Assert.AreEqual(first.position.z, last.position.z, 1e-4f, "같은 거리");
            Assert.Greater(Vector3.Dot(first.rotation * Vector3.back, Vector3.back), 0.99f, "앞면(-Z)이 머리 쪽");
        }

        // 결과판 · 더미 정보 문구 (RevealText): CardRevealer에서 떼어 낸 순수 문자열 함수
        [Test]
        public void BoxRow_SpecialPack_ShowsLabelAndEndsWithNewline()
        {
            string row = RevealText.BoxRow(Pack(new[] { U, U, U, U, U }, special: SpecialPackType.GodPack), 3, null);
            StringAssert.Contains("갓팩!", row);
            StringAssert.EndsWith("\n", row);
        }

        [Test]
        public void PileInfo_CountsCardsNewAndRarities()
        {
            PackResult p = Pack(new[] { N, S, L });
            string info = RevealText.PileInfo(p.Cards, 1, p.Cards[2], true, null);
            StringAssert.Contains("테이블 카드 더미 3장", info);
            StringAssert.Contains("새 카드 1장", info);
            StringAssert.Contains("Legendary 1", info);
            StringAssert.Contains("NEW", info, "집은 카드가 새 카드면 NEW 표시");
        }
    }
}
