using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 바인더가 생기기 전의 임시 보관함 연출: 더미 카드가 맨 위 카드부터 조금씩 시차를 두고 솟아오르며 작아져 사라집니다.
    /// MonoBehaviour가 아니라 CardRevealer가 값만 넘겨 만듭니다.
    /// </summary>
    public sealed class FlyAwayCardStash : ICardStash
    {
        readonly float rise;
        readonly float seconds;
        readonly float stagger;

        /// <param name="rise">위로 날아가는 거리 (m)</param>
        /// <param name="seconds">한 장이 날아가는 시간 (초)</param>
        /// <param name="stagger">카드 사이 출발 시차 (초)</param>
        public FlyAwayCardStash(float rise, float seconds, float stagger)
        {
            this.rise = rise;
            this.seconds = Mathf.Max(0.05f, seconds);
            this.stagger = Mathf.Max(0f, stagger);
        }

        public float Send(IReadOnlyList<CardRevealView> pile)
        {
            int n = pile.Count;
            for (int i = 0; i < n; i++)
            {
                CardRevealView card = pile[n - 1 - i]; // 맨 위 카드부터
                Transform t = card.transform;
                card.FlyTo(t.position + Vector3.up * rise, t.rotation, seconds, toScale: 0.05f,
                    arcHeight: 0f, grabbableOnArrive: false, delay: stagger * i);
            }
            return n == 0 ? 0f : seconds + stagger * (n - 1);
        }
    }
}
