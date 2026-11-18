using System.Collections.Generic;
using CardShop.Core;
using CardShop.Presentation;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 집에서만 보는 신호 두 가지를 만듭니다 (게임 코드를 고치지 않고 읽기만 함).
    /// ① 바인더 페이지 · 탭이 바뀜 → BinderBrowsed ② 판매 등록할 수 있는(수퍼레어 이상) 카드를 가지고 있음 → DisplayableCardOwned (1초마다 확인).
    /// </summary>
    public sealed class TutorialHomeWatch
    {
        int page = -1, tab = -1;
        float untilCheck;

        /// <summary>집에 새로 들어왔을 때: 바인더 기준값을 다시 잡음</summary>
        public void Reset()
        {
            page = tab = -1;
            untilCheck = 0f;
        }

        /// <summary>이번 프레임에 보낼 신호 (없으면 None). 둘 다 해당되면 바인더 쪽이 먼저, 다른 하나는 다음 프레임에.</summary>
        public TutorialSignal Tick(BinderView binder, GameContext ctx, float deltaTime)
        {
            if (binder != null)
            {
                int p = binder.Page, t = (int)binder.Tab;
                bool changed = page >= 0 && (p != page || t != tab);
                page = p; tab = t;
                if (changed) return TutorialSignal.BinderBrowsed;
            }

            untilCheck -= deltaTime;
            if (untilCheck > 0f || ctx == null) return TutorialSignal.None;
            untilCheck = 1f;
            foreach (KeyValuePair<string, int> kv in ctx.Session.Collection.Owned)
                if (kv.Value > 0 && ctx.Sales.CanDisplay(ctx.Cards.Get(kv.Key))) return TutorialSignal.DisplayableCardOwned;
            return TutorialSignal.None;
        }
    }
}
