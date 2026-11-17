using System;
using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 계산대 하나 (계산시스템_확장_설계 6장). 줄 맨 앞 손님의 장바구니를 카운터에 올리고, 바코드 찍기 · 포스기 화면 · 결제를 맡습니다.
    /// 줄 · 동선은 CustomerDirector가 맡고, 여기는 Begin(장바구니 시작) → TryPay(결제 버튼) → 완료 콜백만 주고받습니다.
    /// 예전에는 CustomerDirector 안에 있던 코드(PrepareCheckout · ServeFront · ClearCheckout)를 옮겨 왔습니다.
    /// </summary>
    public sealed class CheckoutCounter : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [SerializeField] ShopItemConfig shopItems;
        [Tooltip("손님이 물건을 올려놓는 카운터 위 자리들. 물건이 더 많으면 맨 앞 자리부터 위로 쌓음")]
        [SerializeField] Transform[] itemSpots;
        [Tooltip("판매 등록 카드 (CardView 포함, 산 카드로 그림이 바뀜)")]
        [SerializeField] ScannableItem cardItemPrefab;
        [SerializeField] PosTerminal pos;

        /// <summary>결제 완료: (손님이 산 판매 등록 카드 또는 null, 물건 개수)</summary>
        public delegate void PaidHandler(CardData boughtCard, int itemCount);

        GameContext context;
        Basket basket;
        PaidHandler onPaid;
        readonly List<ScannableItem> spawned = new List<ScannableItem>();

        /// <summary>지금 계산 중인 손님이 있는가</summary>
        public bool IsBusy => basket != null;
        /// <summary>지금 장바구니의 물건 수 (인내 시간 계산용)</summary>
        public int ItemCount => basket != null ? basket.Count : 0;

        GameContext Context
        {
            get
            {
                if (context == null) context = flow != null ? flow.GetComponent<GameContext>() : GameContext.Instance;
                return context;
            }
        }

        /// <summary>새 손님의 장바구니를 정해 카운터에 올립니다. 이전 계산은 지웁니다.</summary>
        public void Begin(PaidHandler paid)
        {
            Clear();
            GameContext ctx = Context;
            GameSession s = ctx.Session;

            CardData card = ctx.Sales.PickPurchase(s);
            int cardPrice = card != null ? ctx.Sales.CustomerPrice(card, ctx.PopularToday) : 0;
            List<StockOption> options = shopItems != null ? shopItems.Options() : new List<StockOption>();
            int count = shopItems != null ? BasketRules.RollCount(shopItems.countWeights, shopItems.MaxCount(s.Day), ctx.Random) : 1;
            basket = BasketRules.Build(card, cardPrice, count, options, s.Day, ctx.Random);
            onPaid = paid;

            for (int i = 0; i < basket.Count; i++)
            {
                BasketLine line = basket.Lines[i];
                ScannableItem item = Spawn(line, i);
                spawned.Add(item);
                if (item == null) { basket.MarkScanned(i); continue; } // 프리팹이 없으면 찍을 수 없으니 찍은 것으로 (멈춤 방지)
                int index = i;
                item.Scanned += _ => OnScanned(index);
            }
            if (pos != null) pos.Show(basket);
        }

        ScannableItem Spawn(BasketLine line, int i)
        {
            ScannableItem prefab = line.Card != null ? cardItemPrefab
                : shopItems != null && line.StockIndex >= 0 && line.StockIndex < shopItems.items.Count ? shopItems.items[line.StockIndex].prefab
                : null;
            if (prefab == null || itemSpots == null || itemSpots.Length == 0) return null;

            Transform spot = itemSpots[i % itemSpots.Length];
            Vector3 stack = Vector3.up * (0.03f * (i / itemSpots.Length));
            ScannableItem item = Instantiate(prefab, spot.position + stack, spot.rotation, transform);
            if (line.Card != null)
            {
                var view = item.GetComponentInChildren<CardView>();
                if (view != null) view.Bind(line.Card);
            }
            return item;
        }

        void OnScanned(int index)
        {
            if (basket == null || !basket.MarkScanned(index)) return;
            BasketLine line = basket.Lines[index];
            ScannableItem item = spawned[index];
            if (item != null) item.ShowLabel($"{line.Name}\n{line.Price}");
            if (pos != null) pos.Show(basket);
        }

        /// <summary>결제 버튼(Poke). 장바구니의 물건을 모두 찍었어야 합니다.</summary>
        public void TryPay()
        {
            if (flow == null || flow.CurrentPhase != GamePhaseId.Business || flow.IsPaused) return;
            GameContext ctx = Context;
            if (basket == null)
            {
                ctx.Events.RaiseNotice("결제할 손님이 없어요.");
                return;
            }
            if (!basket.AllScanned)
            {
                ctx.Events.RaiseNotice($"먼저 스캐너로 물건의 바코드를 찍어 주세요. ({basket.ScannedCount}/{basket.Count})");
                return;
            }

            CardData card = basket.Card;
            int count = basket.Count;
            PaidHandler paid = onPaid;
            // 진열 카드면 진열대에서 빼서 장부에 기록. 그새 진열대에서 사라졌으면 가게 재고를 산 것으로 봄 (SalesRules)
            CardData bought = ctx.Sales.CompleteSale(ctx.Session, card, ctx.PopularToday) ? card : null;
            Clear(); // 손님이 물건을 가져감
            paid?.Invoke(bought, count);
        }

        /// <summary>손님이 기다리다 떠나거나 영업이 끝남: 물건을 치우고 판매 없음.</summary>
        public void Cancel() => Clear();

        void Clear()
        {
            foreach (ScannableItem item in spawned)
                if (item != null) Destroy(item.gameObject);
            spawned.Clear();
            basket = null;
            onPaid = null;
            if (pos != null) pos.Clear();
        }
    }
}
