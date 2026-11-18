using System;
using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Presentation
{
    /// <summary>
    /// 카운터 카드 더미의 장부 (순수 C#, EditMode 테스트). 카드 오브젝트를 움직이지 않고 "몇 번째 높이에 놓일지 · 어떤 카드를 풀로 돌릴지"만 셉니다.
    /// CardRevealer가 카드를 날리고 숨기는 일을 맡습니다. T = 카드 오브젝트 (게임에서는 CardRevealView).
    /// - 단일 팩: 넘긴 카드를 모두 <see cref="Pile"/>에 남기고, 오래된 카드는 <see cref="Compress"/>로 옆면 덩어리(<see cref="TableCards"/>)로 줄입니다.
    /// - 상자: 위쪽 몇 장만 실제 카드로 두고(<see cref="TrimBox"/>), 아래는 덩어리 높이(<see cref="BlockTop"/>)로만 셉니다.
    /// 지켜야 할 규칙: 단일 팩 카드만 있을 때 NextIndex = TableCards.Count + Pile.Count (다음 카드는 맨 위에 쌓임).
    /// </summary>
    public sealed class CounterPile<T> where T : class
    {
        readonly struct BoxEntry
        {
            public readonly T Item;
            public readonly int Index;
            public readonly float ArriveAt;
            public BoxEntry(T item, int index, float arriveAt) { Item = item; Index = index; ArriveAt = arriveAt; }
        }

        readonly Func<T, CardData> cardOf;
        readonly Func<T, bool> isNewOf;
        readonly Func<T, bool> isHeld;
        readonly List<T> pile = new List<T>();
        readonly List<CardData> tableCards = new List<CardData>();
        readonly List<BoxEntry> box = new List<BoxEntry>();

        public CounterPile(Func<T, CardData> cardOf, Func<T, bool> isNewOf, Func<T, bool> isHeld)
        {
            this.cardOf = cardOf;
            this.isNewOf = isNewOf;
            this.isHeld = isHeld;
        }

        /// <summary>더미에 놓인(또는 건너뛰어 놓인 것으로 친) 카드 수 = 다음 카드의 높이 순번.</summary>
        public int NextIndex { get; private set; }
        /// <summary>옆면 덩어리 하나로 표시하는 아래쪽 카드 수 (0이면 덩어리 없음).</summary>
        public int BlockTop { get; private set; }
        /// <summary>단일 팩: 카운터에 실제 카드로 남은 것 (아래부터).</summary>
        public IReadOnlyList<T> Pile => pile;
        /// <summary>덩어리로 줄인 오래된 카드 (보관함 알림 · 더미 정보용).</summary>
        public IReadOnlyList<CardData> TableCards => tableCards;
        /// <summary>덩어리로 줄인 카드 중 새 카드 수.</summary>
        public int TableNew { get; private set; }
        /// <summary>퇴근 전까지 테이블에 쌓아 둔 단일 팩 카드 수 (덩어리 포함).</summary>
        public int CardCount => tableCards.Count + pile.Count;
        public bool HasCards => CardCount > 0;

        /// <summary>카드 하나를 맨 위에 놓고 그 높이 순번을 돌려줍니다. 상자 카드는 arriveAt(내려앉는 시각)을 기억해 두었다가 TrimBox에서 줄입니다.</summary>
        public int Place(T item, bool isBox, float arriveAt)
        {
            int index = NextIndex++;
            if (isBox) box.Add(new BoxEntry(item, index, arriveAt));
            else pile.Add(item);
            return index;
        }

        /// <summary>건너뛴 상자 카드: 오브젝트 없이 높이로만 셈 (덩어리가 그만큼 자람).</summary>
        public void CountSkipped(int count)
        {
            NextIndex += count;
            BlockTop = Math.Max(BlockTop, NextIndex);
        }

        /// <summary>상자 더미: 다 내려앉은(arriveAt ≤ now) 아래쪽 카드부터 visible장만 남기고 recycled에 담습니다. 그 높이만큼 덩어리가 자랍니다.</summary>
        public void TrimBox(float now, int visible, List<T> recycled)
        {
            while (box.Count > visible && box[0].ArriveAt <= now)
            {
                BoxEntry e = box[0];
                box.RemoveAt(0);
                BlockTop = Math.Max(BlockTop, e.Index + 1);
                recycled.Add(e.Item);
            }
        }

        /// <summary>단일 팩 더미: visible장을 넘으면 가장 오래된 카드부터 덩어리로 줄이고 recycled에 담습니다. 손에 든 카드에서 멈춥니다.</summary>
        public void Compress(int visible, List<T> recycled)
        {
            while (pile.Count > visible)
            {
                T item = pile[0];
                if (isHeld(item)) break;
                pile.RemoveAt(0);
                tableCards.Add(cardOf(item));
                if (isNewOf(item)) TableNew++;
                BlockTop = tableCards.Count;
                recycled.Add(item);
            }
        }

        /// <summary>
        /// 상자 카드(실제 카드 + 덩어리)를 치웁니다. 단일 팩 더미는 그대로.
        /// keepBlock이 아니면 덩어리를 없애고, 다음 카드가 남은 단일 팩 카드 바로 위에 쌓이게 높이 순번을 맞춥니다
        /// (상자 뒤 단일 팩이 공중에 쌓이던 문제).
        /// </summary>
        public void ClearBox(List<T> recycled, bool keepBlock)
        {
            foreach (BoxEntry e in box) recycled.Add(e.Item);
            box.Clear();
            if (keepBlock) return;
            BlockTop = 0;
            NextIndex = CardCount;
        }

        /// <summary>보관함으로 보냄: 실제 카드(batch)와 덩어리를 포함한 카드 전체(cards, 아래부터)를 내주고 비웁니다.</summary>
        public void TakeAll(List<T> batch, List<CardData> cards)
        {
            cards.AddRange(tableCards);
            foreach (T item in pile)
            {
                batch.Add(item);
                cards.Add(cardOf(item));
            }
            // 단일 팩 더미만 비움. 날아가는 중인 상자 카드(box)는 ClearBox가 풀로 돌려보내야 하므로 남겨 둠
            pile.Clear();
            tableCards.Clear();
            TableNew = 0;
            BlockTop = 0;
            NextIndex = 0;
        }

        /// <summary>테이블 카드 전체(덩어리 포함)와 그중 새 카드 수 (더미 정보 표시용).</summary>
        public int CollectCards(List<CardData> cards)
        {
            cards.AddRange(tableCards);
            int newCount = TableNew;
            foreach (T item in pile)
            {
                cards.Add(cardOf(item));
                if (isNewOf(item)) newCount++;
            }
            return newCount;
        }

        public bool Contains(T item) => pile.Contains(item);

        /// <summary>모두 비움 (상자 실제 카드는 부르는 쪽이 먼저 ClearBox로 거둬야 함).</summary>
        public void Reset()
        {
            pile.Clear();
            tableCards.Clear();
            box.Clear();
            TableNew = 0;
            BlockTop = 0;
            NextIndex = 0;
        }
    }
}
