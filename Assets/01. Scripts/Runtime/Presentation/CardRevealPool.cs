using System;
using System.Collections.Generic;
using CardShop.Cards;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 공개용 카드 오브젝트 재사용 (Quest 성능: TMP · Rigidbody · XRGrabInteractable 카드를 매번 만들지 않음).
    /// 화면에 나온 카드(<see cref="Shown"/>)와 쉬는 카드를 따로 들고, 만드는 방법은 CardRevealer가 넘겨줍니다.
    /// </summary>
    public sealed class CardRevealPool
    {
        readonly Func<CardRevealView> create;
        readonly Stack<CardRevealView> free = new Stack<CardRevealView>();
        readonly List<CardRevealView> shown = new List<CardRevealView>();

        public CardRevealPool(Func<CardRevealView> create) => this.create = create;

        /// <summary>지금까지 만든 카드 수 (미리 데우기 목표와 비교).</summary>
        public int CreatedCount { get; private set; }

        /// <summary>쉬는 카드를 꺼내(없으면 새로 만들어) 보여 줍니다.</summary>
        public CardRevealView Show(CardData card, bool isNew, Vector3 position, Quaternion rotation, float scale)
        {
            CardRevealView view = free.Count > 0 ? free.Pop() : Make();
            view.Show(card, isNew, position, rotation, scale);
            shown.Add(view);
            return view;
        }

        /// <summary>화면에서 빼서 숨기고 쉬게 합니다.</summary>
        public void Recycle(CardRevealView view)
        {
            if (view == null) return;
            shown.Remove(view);
            Rest(view);
        }

        /// <summary>화면 목록에서만 뺌 (보관함으로 날아가는 중인 카드: 연출이 끝나면 <see cref="Rest"/>).</summary>
        public void Detach(CardRevealView view) => shown.Remove(view);

        /// <summary>목록에 없는 카드를 숨기고 쉬게 합니다.</summary>
        public void Rest(CardRevealView view)
        {
            view.Hide();
            free.Push(view);
        }

        /// <summary>keep이 true인 카드만 남기고 화면의 카드를 모두 쉬게 합니다.</summary>
        public void RecycleAll(Predicate<CardRevealView> keep = null)
        {
            for (int i = shown.Count - 1; i >= 0; i--)
            {
                CardRevealView v = shown[i];
                if (keep != null && keep(v)) continue;
                shown.RemoveAt(i);
                Rest(v);
            }
        }

        /// <summary>미리 하나 만들어 쉬게 둠 (퇴근 후 데우기).</summary>
        public void AddSpare() => Rest(Make());

        CardRevealView Make()
        {
            CreatedCount++;
            return create();
        }
    }
}
