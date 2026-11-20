using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using CardShop.Presentation;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 대사 자리표시 값 채우기 (튜토리얼_설계 13-4). 판마다 같은 값({packPrice} 등)과,
    /// 단계가 뜰 때마다 바뀌는 값({pickedShopPrice} · {top1Strength} 등)을 함께 채웁니다.
    /// TutorialDirector가 말풍선을 띄울 때마다 부릅니다 (Director 300줄 점검선 때문에 따로 둠, AI-005).
    /// </summary>
    public static class TutorialLineValues
    {
        public static Dictionary<string, string> Fill(Dictionary<string, string> values, GameContext ctx, BinderView binder)
        {
            if (ctx == null) return values;
            EconomyConfig e = ctx.Economy;
            values["packPrice"] = MoneyText.Format(e.packPrice);
            values["boxPrice"] = MoneyText.Format(e.boxPrice);
            values["cleanupBonus"] = e.cleanupBonus.ToString();
            values["cleanupPenalty"] = e.cleanupPenalty.ToString();
            values["closingCleanupSeconds"] = ctx.Campaign.closingCleanupSeconds.ToString("0");

            // 경제 · 가구 (고정값)
            float normal = ctx.Cards != null ? NormalBuyback(ctx) : 0.2f;
            values["normalBulkCount"] = normal > 0f ? Mathf.RoundToInt(1f / normal).ToString() : "5";
            values["normalBulkPrice"] = MoneyText.Format(1);
            values["setBonus"] = ctx.Score.pointsPerShowcaseSet.ToString();
            values["maxSets"] = ShowcaseSetRules.MaxSets.ToString();

            if (ctx.Session == null || ctx.Market == null) return values;
            // 오늘 시세
            MarketDay today = ctx.MarketToday;
            values["top1Element"] = CommunityBoard.ElementName(today.Primary);
            values["top1Strength"] = today.HasPopular ? today.TierName : "상승";
            values["fallElement"] = CommunityBoard.ElementName(today.Falling);

            // 고른 카드 (집에서 바인더로 고른 카드)
            CardData picked = binder != null ? binder.SelectedCard : null;
            values["pickedShopPrice"] = picked != null ? MoneyText.Format(ctx.EconomyRules.SellPrice(picked, ctx.Market.For(ctx.Session.Day + 1))) : MoneyText.Format(0);
            values["pickedBuyback"] = picked != null ? MoneyText.Format(ctx.Sales.InstantPrice(picked, ctx.Session)) : MoneyText.Format(0);

            // 진열장 세트 · 꾸미기
            var showcase = new List<CardData>();
            foreach (string id in ctx.Session.Showcase) showcase.Add(ctx.Cards.Get(id));
            List<CardElement> sets = ShowcaseSetRules.Sets(showcase);
            values["setElement"] = sets.Count > 0 ? CommunityBoard.ElementName(sets[sets.Count - 1]) : "";
            values["decorCount"] = DecorCount(ctx.Session.Upgrades).ToString();
            return values;
        }

        static float NormalBuyback(GameContext ctx)
        {
            foreach (CardData c in ctx.Cards.cards)
                if (c != null && c.rarity == CardRarity.Normal) return ctx.Sales.InstantValue(c, ctx.Session);
            return 0.2f;
        }

        static int DecorCount(UpgradeState up)
        {
            int n = 0;
            foreach (UpgradeKind k in new[] { UpgradeKind.Poster, UpgradeKind.Rug, UpgradeKind.Lamp, UpgradeKind.Figure, UpgradeKind.AirCon })
                if (up.LevelOf(k) > 1) n++;
            return n;
        }
    }
}
