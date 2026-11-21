using System;
using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using CardShop.Presentation;
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
        [Tooltip("판매 등록 카드가 계산대에 올라올 때 크기 배율 (11-19 피드백: 너무 작게 보임 → 1.2)")]
        [SerializeField] float cardItemScale = 1.2f;
        [SerializeField] PosTerminal pos;
        [Tooltip("찍은 물건을 담는 종이봉투 (계산시스템_확장_설계 K2). 비우면 봉투 없이 다 찍기만 하면 결제(예전 방식)")]
        [SerializeField] PaperBag bag;

        /// <summary>결제 완료: (손님이 산 판매 등록 카드 또는 null, 물건 개수)</summary>
        public delegate void PaidHandler(CardData boughtCard, int itemCount, int net);

        GameContext context;
        Basket basket;
        PaidHandler onPaid;
        float checkoutSeconds; // 물건을 올린 뒤 흐른 시간 (일시정지 제외), 계산 평가용
        readonly List<ScannableItem> spawned = new List<ScannableItem>();

        /// <summary>다음 손님 한 명의 물건 수를 이 값으로 고정 (0 = 평소대로). 한 번 쓰면 0으로 돌아갑니다 (튜토리얼 첫 손님 1개, 튜토리얼_설계 9장 Q2).</summary>
        public int ForceNextItemCount { get; set; }

        /// <summary>지금 장바구니의 물건 수 (인내 시간 계산용)</summary>
        public int ItemCount => basket != null ? basket.Count : 0;

        /// <summary>지금 계산대에 올라온 판매 등록 카드 (없으면 null). 진열대 카드를 빼는 데 씀 (StandListedCards, 11-21 P1)</summary>
        public CardData CardOnCounter => basket != null ? basket.Card : null;

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

            CardData card = ctx.Sales.PickPurchase(s, ctx.MarketToday);
            int cardPrice = card != null ? ctx.Sales.CustomerPrice(card, ctx.MarketToday) : 0;
            List<StockOption> options = shopItems != null ? shopItems.Options() : new List<StockOption>();
            int count = shopItems != null ? BasketRules.RollCount(shopItems.countWeights, shopItems.MaxCount(s.Day), ctx.Random) : 1;
            if (ForceNextItemCount > 0) { count = ForceNextItemCount; ForceNextItemCount = 0; }
            basket = BasketRules.Build(card, cardPrice, count, options, s.Day, ctx.Random);
            onPaid = paid;
            checkoutSeconds = 0f;

            for (int i = 0; i < basket.Count; i++)
            {
                BasketLine line = basket.Lines[i];
                ScannableItem item = Spawn(line, i);
                spawned.Add(item);
                if (item == null) { basket.MarkScanned(i); basket.MarkBagged(i); continue; } // 프리팹이 없으면 찍을 수 없으니 담은 것으로 (멈춤 방지)
                int index = i;
                item.CanScan = () => !Paused;
                item.Scanned += _ => OnScanned(index);
                item.Released += released => OnReleased(index, released);
            }
            if (pos != null) pos.Show(basket);
            ctx.Events.RaiseHappened(GameMoment.CheckoutStarted);
            if (card != null) ctx.Events.RaiseHappened(GameMoment.ListedCardCheckout);
        }

        void Update()
        {
            if (basket != null && flow != null && !flow.IsTimeStopped) checkoutSeconds += Time.deltaTime; // 튜토리얼 정지 중에도 빠른 계산 시간은 멈춤
        }

        ScannableItem Spawn(BasketLine line, int i)
        {
            bool stock = line.Card == null && shopItems != null && line.StockIndex >= 0 && line.StockIndex < shopItems.items.Count;
            ScannableItem prefab = line.Card != null ? cardItemPrefab : stock ? shopItems.items[line.StockIndex].prefab : null;
            float scale = line.Card != null ? cardItemScale : stock ? shopItems.items[line.StockIndex].counterScale : 1f;
            if (prefab == null || itemSpots == null || itemSpots.Length == 0) return null;

            // 자리 Transform의 위치 = 물건 바닥이 닿는 면. 씬에서 자리를 옮기면 물건도 그 자리 · 높이에 놓임.
            // 물건이 자리보다 많으면 같은 자리 위로 앞 물건 높이만큼 쌓음
            Transform spot = itemSpots[i % itemSpots.Length];
            float stack = 0f;
            for (int k = i - itemSpots.Length; k >= 0; k -= itemSpots.Length)
                if (spawned[k] != null) stack += spawned[k].Height + 0.002f;
            ScannableItem item = Instantiate(prefab, spot.position, spot.rotation, transform);
            if (scale > 0f && !Mathf.Approximately(scale, 1f)) item.transform.localScale *= scale; // 놓기 · 쌓기 높이는 바뀐 크기로 계산
            item.PlaceOn(spot.position + spot.up * stack, spot.rotation);
            if (line.Card != null)
            {
                var view = item.GetComponentInChildren<CardView>();
                if (view != null) view.Bind(line.Card);
            }
            return item;
        }

        // 일시정지 중에는 계산 시간이 멈추므로 찍기 · 담기도 받지 않음 (멈춘 채 계산해 빠른 계산 보너스를 받는 일 방지)
        bool Paused => flow != null && flow.IsPaused;

        void OnScanned(int index)
        {
            if (Paused || basket == null || !basket.MarkScanned(index)) return;
            BasketLine line = basket.Lines[index];
            ScannableItem item = spawned[index];
            if (item != null) item.ShowLabel($"{line.Name}\n{MoneyText.Format(line.Price)}");
            if (bag == null) basket.MarkBagged(index); // 봉투가 없는 씬: 찍으면 끝
            if (pos != null) pos.Show(basket);
            Context.Events.RaiseHappened(GameMoment.ItemScanned);
            if (basket.AllScanned) Context.Events.RaiseHappened(GameMoment.AllScanned);
            if (basket.AllBagged) Context.Events.RaiseHappened(GameMoment.AllBagged);
        }

        // 물건을 놓은 곳이 봉투 위면 담기. 안 찍은 물건은 받지 않고 안내만 (물건은 스스로 제자리로 돌아감)
        void OnReleased(int index, ScannableItem item)
        {
            if (Paused || basket == null || bag == null || item == null || !bag.Contains(item.transform.position)) return;
            if (!basket.Lines[index].Scanned)
            {
                Context.Events.RaiseNotice("먼저 바코드를 찍어 주세요.");
                return;
            }
            if (!basket.MarkBagged(index)) return;
            bag.Put(item);
            Sfx.Play(SoundId.BagPut, item.transform.position);
            if (pos != null) pos.Show(basket);
            if (basket.AllBagged) Context.Events.RaiseHappened(GameMoment.AllBagged);
        }

        /// <summary>결제 버튼(Poke). 장바구니의 물건을 모두 찍고 봉투에 담았어야 합니다.</summary>
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
            if (!basket.AllBagged)
            {
                ctx.Events.RaiseNotice($"아직 봉투에 안 담은 물건이 있어요. ({basket.BaggedCount}/{basket.Count})");
                return;
            }

            CardData card = basket.Card;
            int count = basket.Count;
            PaidHandler paid = onPaid;
            if (ctx.EconomyRules.IsFastCheckout(checkoutSeconds, count)) ctx.Session.Ledger.checkoutsFast++;
            // 진열 카드면 진열대에서 빼서 장부에 기록. 그새 진열대에서 사라졌으면 가게 재고를 산 것으로 봄 (SalesRules)
            CardData bought = ctx.Sales.CompleteSale(ctx.Session, card, ctx.MarketToday, out int net) ? card : null;
            Sfx.Play(SoundId.PosPay, pos != null ? pos.transform.position : transform.position);
            Sfx.Play(SoundId.MoneyIn, pos != null ? pos.transform.position : transform.position); // 동전 "띠링" (11-20 G1)
            Clear(); // 손님이 물건을 가져감
            ctx.Events.RaiseHappened(GameMoment.CheckoutPaid);
            paid?.Invoke(bought, count, net);
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
            if (bag != null) bag.Clear();
            if (pos != null) pos.Clear();
        }
    }
}
