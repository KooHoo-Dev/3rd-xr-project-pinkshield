using System;
using CardShop.Cards;
using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 청소 구역 하나 (설계서 7-4). 손님이 나갈 때 CleanupDirector가 만들고, 치우면 Completed를 한 번 알립니다.
    /// 영업이 끝나면 Discard로 걷어 냅니다(치우지 못한 수는 Director가 셈).
    /// </summary>
    public abstract class CleanupSpot : MonoBehaviour
    {
        public bool IsDone { get; private set; }
        public event Action<CleanupSpot> Completed;

        protected void Complete()
        {
            if (IsDone) return;
            IsDone = true;
            Completed?.Invoke(this);
        }

        /// <summary>영업 끝: 장면에서 걷어 냅니다. 기본은 오브젝트 삭제.</summary>
        public virtual void Discard() => Destroy(gameObject);
    }
}
