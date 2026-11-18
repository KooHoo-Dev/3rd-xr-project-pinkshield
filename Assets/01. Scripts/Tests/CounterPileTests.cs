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

        // 고친 버그: 상자를 연 뒤 같은 퇴근 시간에 단일 팩을 열면 카드가 상자 높이(약 10cm) 위에 떠서 쌓였음
        [Test]
        public void BoxThenSinglePack_StartsFromCounter()
        {
            CounterPile<Card> pile = NewPile();
            var recycled = new List<Card>();
            for (int i = 0; i < 20; i++) pile.Place(new Card(), true, 0f);
            pile.CountSkipped(30);
            Assert.AreEqual(50, pile.NextIndex);

            pile.ClearBox(recycled, keepBlock: false); // 상자 결과판으로 넘어갈 때
            Assert.AreEqual(20, recycled.Count, "상자 카드는 모두 풀로");
            Assert.AreEqual(0, pile.BlockTop);
            Assert.AreEqual(0, pile.Place(new Card(), false, 0f), "다음 단일 팩 카드는 카운터 바닥부터");
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
