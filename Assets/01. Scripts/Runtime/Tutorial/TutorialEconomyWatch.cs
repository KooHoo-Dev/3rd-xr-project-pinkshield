using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using CardShop.Presentation;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 경제 · 가구 튜토리얼 신호 중 "읽어서 판단"하는 것 (튜토리얼_설계 13-4). 게임 코드를 고치지 않고 1초마다 상태를 읽습니다
    /// (TutorialHomeWatch와 같은 방식). 신호는 조건이 맞는 동안 계속 보내도 되고(단계는 한 번만 시작), 페이즈마다 맞는 것만 봅니다.
    /// </summary>
    public sealed class TutorialEconomyWatch
    {
        readonly List<TutorialSignal> found = new List<TutorialSignal>();
        float untilCheck;
        int lastDecor = -1;
        bool hadPoster, hadStall;
        bool sawSurge; // 이번 판에 급등 이상 날을 겪었나 (집 대비책 안내를 생략하려고)

        /// <summary>새 판 (세트 · 꾸미기 기준값을 다시 잡음)</summary>
        public void Reset()
        {
            untilCheck = 0f;
            lastDecor = -1;
            hadPoster = hadStall = sawSurge = false;
        }

        /// <summary>이번에 보낼 신호들 (1초에 한 번만 채움, 그 사이엔 빈 목록)</summary>
        public List<TutorialSignal> Tick(GameContext ctx, BinderView binder, GamePhaseId phase, float deltaTime)
        {
            found.Clear();
            untilCheck -= deltaTime;
            if (untilCheck > 0f || ctx == null || ctx.Session == null || ctx.Market == null) return found;
            untilCheck = 1f;

            GameSession s = ctx.Session;
            switch (phase)
            {
                case GamePhaseId.Business: Business(ctx, s); break;
                case GamePhaseId.Settlement: Settlement(ctx, s); break;
                case GamePhaseId.Home: Home(ctx, s, binder); break;
            }
            if (ctx.Upgrades.ForecastDays(s.Upgrades) >= 2) found.Add(TutorialSignal.ForecastStrengthVisible);
            return found;
        }

        void Business(GameContext ctx, GameSession s)
        {
            MarketDay today = ctx.MarketToday;
            if (s.SalesDisplay.Count > 0) found.Add(TutorialSignal.ListedCardsOnShelf);
            if (today.TierIndex >= SurgeTier) { found.Add(TutorialSignal.MarketSurgeToday); sawSurge = true; }
            if (today.TierIndex >= ctx.Economy.popularTiers.Length) found.Add(TutorialSignal.RecordSurgeToday); // 연장 날의 "역대급" (표 끝 다음)
            if (today.Falling != CardElement.None) found.Add(TutorialSignal.MarketFallingToday);
            if (s.Endless.Active && s.Day == ctx.Campaign.DayCount + 1) found.Add(TutorialSignal.EndlessFirstDay);
        }

        void Settlement(GameContext ctx, GameSession s)
        {
            if (ctx.EconomyRules.SalesTotal(s.Ledger) > 0) found.Add(TutorialSignal.ListedSaleSettled);
            if (UpgradeCredits.Line(s.Ledger) != null) found.Add(TutorialSignal.UpgradeCreditShown);
        }

        void Home(GameContext ctx, GameSession s, BinderView binder)
        {
            CardData picked = binder != null ? binder.SelectedCard : null;
            if (picked != null && ctx.Sales.CanDisplay(picked)) found.Add(TutorialSignal.RareCardPicked);

            UpgradeState up = s.Upgrades;
            if (s.SalesDisplay.Count >= ctx.Sales.SlotCount(s) && up.LevelOf(UpgradeKind.Binder) < ctx.Upgrades.MaxLevel(UpgradeKind.Binder))
                found.Add(TutorialSignal.SalesSlotsFull);

            // 진열장 세트: 지금 세운 세트 수가 늘면 완성, 보유 + 진열장으로 세트를 만들 수 있으면 "가능"
            var showcase = new List<CardData>();
            foreach (string id in s.Showcase) showcase.Add(ctx.Cards.Get(id));
            // 세트가 하나라도 있으면 "완성" (첫 완성 안내 · 세트 준비 안내 생략에 씀)
            if (ShowcaseSetRules.SetCount(showcase) > 0) found.Add(TutorialSignal.ShowcaseSetCompleted);
            if (sawSurge) found.Add(TutorialSignal.MarketSurgeToday); // 집 5일차 강도 대비책은 이미 급등을 봤으면 생략
            var all = new List<CardData>(showcase);
            foreach (KeyValuePair<string, int> e in s.Collection.Owned)
                for (int i = 0; i < e.Value && i < ShowcaseSetRules.SetSize; i++) all.Add(ctx.Cards.Get(e.Key));
            if (ShowcaseSetRules.SetCount(all) > 0) found.Add(TutorialSignal.ShowcaseSetPossible);

            // 꾸미기 · 포스터 · 아늑한 방 · 슬라임마켓 설치 (상자를 열어 단계가 오른 때)
            int decor = 0;
            foreach (UpgradeKind k in DecorKinds) if (up.LevelOf(k) > 1) decor++;
            if (lastDecor >= 0 && decor > lastDecor) found.Add(TutorialSignal.DecorInstalled);
            lastDecor = decor;
            bool poster = up.LevelOf(UpgradeKind.Poster) > 1;
            if (poster && !hadPoster) found.Add(TutorialSignal.PosterInstalled);
            hadPoster = poster;
            if (ctx.Upgrades.AllDecorOwned(up)) found.Add(TutorialSignal.CozyRoomCompleted);
            bool stall = ctx.Upgrades.HasOnlineStall(up);
            if (stall && !hadStall) found.Add(TutorialSignal.OnlineStallInstalled);
            hadStall = stall;
        }

        /// <summary>강도표에서 "급등"의 번호 (0 소폭 · 1 상승 · 2 급등 · 3 폭등)</summary>
        public const int SurgeTier = 2;

        static readonly UpgradeKind[] DecorKinds =
            { UpgradeKind.Poster, UpgradeKind.Rug, UpgradeKind.Lamp, UpgradeKind.Figure, UpgradeKind.AirCon };
    }
}
