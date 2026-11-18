using System;
using System.Collections.Generic;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 게임 시간 정지 이유 모음 (튜토리얼_설계 8-3). 이유가 하나라도 있으면 IsHeld = true.
    /// 일시정지 메뉴(IsPaused)와는 따로 켜고 끕니다. Changed는 IsHeld가 바뀔 때만 알립니다.
    /// </summary>
    public sealed class TutorialHold
    {
        readonly HashSet<object> reasons = new HashSet<object>();

        public bool IsHeld => reasons.Count > 0;

        public event Action<bool> Changed;

        public void Add(object reason)
        {
            if (reason == null) return;
            bool before = IsHeld;
            reasons.Add(reason);
            RaiseIfFlipped(before);
        }

        public void Remove(object reason)
        {
            if (reason == null) return;
            bool before = IsHeld;
            reasons.Remove(reason);
            RaiseIfFlipped(before);
        }

        public void Clear()
        {
            bool before = IsHeld;
            reasons.Clear();
            RaiseIfFlipped(before);
        }

        void RaiseIfFlipped(bool before)
        {
            if (before != IsHeld) Changed?.Invoke(IsHeld);
        }
    }
}
