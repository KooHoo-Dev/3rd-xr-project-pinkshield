using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Presentation;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>카운터 카드 더미 장부 (CounterPile): 높이 순번 · 덩어리 · 오래된 카드 줄이기 · 상자 뒤 단일 팩.</summary>
    public class CounterPileTests
    {
        sealed class Card
        {
            public bool IsNew;
            public bool Held;
        }

        static CounterPile<Card> NewPile() => new CounterPile<Card>(c => null, c => c.IsNew, c => c.Held);

        static void PlaceSingle(CounterPile<Card> pile, int count, bool isNew = false)
        {
            for (int i = 0; i < count; i++) pile.Place(new Card { IsNew = isNew }, false, 0f);
        }

        [Test]
        public void SinglePack_IndexGrowsByOne()
        {
            CounterPile<Card> pile = NewPile();
            Assert.AreEqual(0, pile.Place(new Card(), false, 0f));
            Assert.AreEqual(1, pile.Place(new Card(), false, 0f));
            Assert.AreEqual(2, pile.NextIndex);
            Assert.AreEqual(2, pile.Pile.Count);
        }

        // 11-19 플레이 피드백: 상자 카드 더미도 카운터에 남기고, 이어서 여는 단일 팩은 그 위에 쌓임
        [Test]
        public void BoxSettles_ThenSinglePackStacksOnTop()
        {
            CounterPile<Card> pile = NewPile();
            var recycled = new List<Card>();
            for (int i = 0; i < 20; i++) pile.Place(new Card(), true, 0f);
            pile.TrimBox(now: 1f, visible: 3, recycled);
            for (int i = 0; i < 30; i++) pile.CountSkipped(null, isNew: i < 4);
            Assert.AreEqual(50, pile.NextIndex);

            pile.SettleBox();
            Assert.AreEqual(17, recycled.Count, "아래 카드는 덩어리로");
            Assert.AreEqual(3, pile.Pile.Count, "위 3장은 실제 카드로 남음");
            Assert.AreEqual(47, pile.TableCards.Count, "덩어리 = 줄인 카드 + 건너뛴 카드");
            Assert.AreEqual(4, pile.TableNew);
            Assert.AreEqual(pile.CardCount, pile.NextIndex, "장부 규칙 유지");
            Assert.AreEqual(50, pile.Place(new Card(), false, 0f), "다음 단일 팩 카드는 상자 더미 바로 위");
        }

        [Test]
        public void BoxSettled_TakeAllIncludesBoxCards()
        {
            CounterPile<Card> pile = NewPile();
            for (int i = 0; i < 10; i++) pile.Place(new Card(), true, 0f);
            pile.TrimBox(now: 1f, visible: 3, new List<Card>());
            pile.SettleBox();
            var batch = new List<Card>();
            var cards = new List<CardData>();
            pile.TakeAll(batch, cards);
            Assert.AreEqual(3, batch.Count);
            Assert.AreEqual(10, cards.Count, "퇴근 때 상자 카드도 보관함으로");
        }

        [Test]
        public void Height_ThinBoxCardsThenThickSingleCards()
        {
            Assert.AreEqual(0.04f, CardPileLayout.Height(50, 50, 0.0008f, 0.002f), 1e-6f, "상자 맨 위");
            Assert.AreEqual(0.044f, CardPileLayout.Height(52, 50, 0.0008f, 0.002f), 1e-6f, "그 위 단일 팩은 보통 두께");
            Assert.AreEqual(0.008f, CardPileLayout.Height(4, 0, 0.0008f, 0.002f), 1e-6f, "상자가 없으면 보통 두께");
        }

        // 11-19 피드백: 더미 자리가 다 차면 마지막 더미에 계속 쌓임 → 한 더미 장수(50)를 넘은 카드는 얇게
        [Test]
        public void Height_AboveSoftCapGrowsThin()
        {
            Assert.AreEqual(0.12f, CardPileLayout.Height(60, 0, 0.0008f, 0.002f, 60), 1e-6f, "60장까지는 보통 두께");
            Assert.AreEqual(0.12f + 40 * 0.0008f, CardPileLayout.Height(100, 0, 0.0008f, 0.002f, 60), 1e-6f, "넘은 40장은 얇게");
            float prev = -1f;
            for (int i = 0; i < 200; i++)
            {
                float h = CardPileLayout.Height(i, 50, 0.0008f, 0.002f, 60);
                Assert.Greater(h, prev, $"{i}번 카드가 아래 카드보다 낮음");
                prev = h;
            }
        }

        [Test]
        public void ClearBox_KeepsSinglePackPileAndStacksOnTop()
        {
            CounterPile<Card> pile = NewPile();
            PlaceSingle(pile, 5);
            pile.ClearBox(new List<Card>(), keepBlock: false);
            Assert.AreEqual(5, pile.NextIndex, "남은 단일 팩 더미 바로 위");
            Assert.AreEqual(5, pile.Pile.Count);
        }

        [Test]
        public void Compress_OldestCardsBecomeBlock_IndexUnchanged()
        {
            CounterPile<Card> pile = NewPile();
            PlaceSingle(pile, 3, isNew: true);
            PlaceSingle(pile, 17);
            var recycled = new List<Card>();

            pile.Compress(15, recycled);

            Assert.AreEqual(5, recycled.Count);
            Assert.AreEqual(15, pile.Pile.Count);
            Assert.AreEqual(5, pile.TableCards.Count);
            Assert.AreEqual(pile.TableCards.Count, pile.BlockTop, "덩어리 높이 = 줄인 카드 수");
            Assert.AreEqual(3, pile.TableNew, "줄인 카드 중 새 카드");
            Assert.AreEqual(20, pile.NextIndex, "다음 카드는 그대로 맨 위");
            Assert.AreEqual(pile.CardCount, pile.NextIndex);
        }

        [Test]
        public void Compress_StopsAtHeldCard()
        {
            CounterPile<Card> pile = NewPile();
            PlaceSingle(pile, 2);
            pile.Pile[0].Held = true;
            PlaceSingle(pile, 3);
            var recycled = new List<Card>();
            pile.Compress(2, recycled);
            Assert.AreEqual(0, recycled.Count, "손에 든 카드(맨 아래)는 줄이지 않음");
        }

        [Test]
        public void TrimBox_KeepsTopCardsAndWaitsForLanding()
        {
            CounterPile<Card> pile = NewPile();
            for (int i = 0; i < 5; i++) pile.Place(new Card(), true, arriveAt: i);
            var recycled = new List<Card>();

            pile.TrimBox(now: 0.5f, visible: 3, recycled);
            Assert.AreEqual(1, recycled.Count, "내려앉은 카드만 (0번)");
            Assert.AreEqual(1, pile.BlockTop);

            pile.TrimBox(now: 10f, visible: 3, recycled);
            Assert.AreEqual(2, recycled.Count, "위 3장은 남김");
            Assert.AreEqual(2, pile.BlockTop);
        }

        [Test]
        public void TakeAll_ReturnsEverythingAndEmpties()
        {
            CounterPile<Card> pile = NewPile();
            PlaceSingle(pile, 20);
            pile.Compress(15, new List<Card>());
            var batch = new List<Card>();
            var cards = new List<CardData>();

            pile.TakeAll(batch, cards);

            Assert.AreEqual(15, batch.Count, "실제 카드 오브젝트");
            Assert.AreEqual(20, cards.Count, "보관함 알림은 덩어리 카드까지 전부");
            Assert.IsFalse(pile.HasCards);
            Assert.AreEqual(0, pile.NextIndex);
            Assert.AreEqual(0, pile.BlockTop);
        }

        // 보관해도 아직 장부에 남은 상자 카드는 버리지 않음 → 이어지는 ClearBox가 풀로 돌려보냄 (카드 누수 방지)
        [Test]
        public void TakeAll_LeavesBoxCardsForClearBox()
        {
            CounterPile<Card> pile = NewPile();
            PlaceSingle(pile, 2);
            pile.Place(new Card(), true, 0f);
            pile.TakeAll(new List<Card>(), new List<CardData>());
            var recycled = new List<Card>();
            pile.ClearBox(recycled, keepBlock: false);
            Assert.AreEqual(1, recycled.Count);
        }
    }
}
