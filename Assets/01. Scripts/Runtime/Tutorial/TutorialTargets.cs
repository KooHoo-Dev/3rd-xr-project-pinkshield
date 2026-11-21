using CardShop.Cards;
using CardShop.Cleanup;
using CardShop.Customers;
using CardShop.Decor;
using CardShop.Interaction;
using CardShop.Presentation;
using CardShop.UI;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 씬 물건에 하이라이트 이름표(HighlightTarget)를 붙입니다. 씬을 다시 빌드하지 않아도 되게 실행 중에 컴포넌트 종류로 찾습니다
    /// (오브젝트 이름 검색은 쓰지 않음, 설계서 7-5). 버튼은 PokeButton에 연결된 함수 이름(직렬화된 이벤트)으로 구분합니다.
    /// 고정 물건은 시작할 때 한 번(MarkFixed), 그때그때 생기는 물건(쓰레기 · 도둑 · 무료 팩)은 단계가 시작될 때(MarkDynamic) 붙입니다.
    /// </summary>
    public static class TutorialTargets
    {
        public static void MarkFixed()
        {
            Mark<ShopClock>(TutorialDefaults.ShopClock);
            Mark<BarcodeScanner>(TutorialDefaults.Scanner);
            Mark<PaperBag>(TutorialDefaults.PaperBag);
            Mark<TrashBin>(TutorialDefaults.TrashBin);
            Mark<PackRack>(TutorialDefaults.PackRack);
            Mark<Broom>(TutorialDefaults.Broom);
            Mark<ShelfController>(TutorialDefaults.PackShelf);
            Mark<SettlementBoard>(TutorialDefaults.SettlementBoard);
            Mark<BinderView>(TutorialDefaults.Binder);
            Mark<SalesPanel>(TutorialDefaults.SalesPanel);
            Mark<CommunityScreen>(TutorialDefaults.Community);

            foreach (PokeButton b in Object.FindObjectsByType<PokeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string key = KeyForButton(b);
                if (key != null) HighlightTarget.Mark(b.gameObject, key);
            }
        }

        /// <summary>단계가 시작될 때: 그사이 새로 생긴 물건에 이름표를 붙입니다. 가끔만 불리므로 Find를 써도 됩니다.</summary>
        public static void MarkDynamic(ThiefDirector thief)
        {
            foreach (TrashSpot t in Object.FindObjectsByType<TrashSpot>(FindObjectsSortMode.None))
                if (!t.IsDone) HighlightTarget.Mark(t.gameObject, TutorialDefaults.Trash);
            // 흐트러진 팩: 기록만 하는 MessyPackSpot(모양 없음) 대신 바닥에 떨어진 팩 하나하나에 (11-20 I1)
            foreach (RackPack p in Object.FindObjectsByType<RackPack>(FindObjectsSortMode.None))
                HighlightTarget.SetMarked(p.gameObject, TutorialDefaults.MessyPack, !p.InRack);
            foreach (StainSpot s in Object.FindObjectsByType<StainSpot>(FindObjectsSortMode.None))
                if (!s.IsDone) HighlightTarget.Mark(s.gameObject, TutorialDefaults.Stain);
            foreach (PackView p in Object.FindObjectsByType<PackView>(FindObjectsSortMode.None))
            {
                if (p.IsFree) HighlightTarget.Mark(p.gameObject, TutorialDefaults.FreePack);
                if (p.IsGift) HighlightTarget.Mark(p.gameObject, TutorialDefaults.GiftPack);
            }
            // 계산대 물건은 계산대 전체가 아니라 지금 다룰 물건 하나하나를 강조. 찍기 · 담기에 따라 이름표를 바꿔 붙임
            foreach (ScannableItem item in Object.FindObjectsByType<ScannableItem>(FindObjectsSortMode.None))
            {
                HighlightTarget.SetMarked(item.gameObject, TutorialDefaults.ItemToScan, !item.IsScanned);
                HighlightTarget.SetMarked(item.gameObject, TutorialDefaults.ItemToBag, item.IsScanned && !item.IsBagged);
            }
            if (thief != null && thief.ActiveThief != null) HighlightTarget.Mark(thief.ActiveThief.gameObject, TutorialDefaults.Thief);

            // 가게 진열대 판매 등록 카드: 오늘 인기 카드 받침 (없으면 줄 전체). 매일 다시 세우므로 단계가 시작될 때 붙임
            var listed = new System.Collections.Generic.List<GameObject>();
            foreach (StandListedCards s in Object.FindObjectsByType<StandListedCards>(FindObjectsSortMode.None))
                s.CollectTutorialTargets(listed);
            foreach (GameObject g in listed) HighlightTarget.Mark(g, TutorialDefaults.ListedCards);

            // 집: 택배 상자 · 진열장 · 꾸미기 (방 업그레이드가 실행 중에 만듦)
            foreach (DeliveryBox b in Object.FindObjectsByType<DeliveryBox>(FindObjectsSortMode.None))
                HighlightTarget.Mark(b.gameObject, TutorialDefaults.DeliveryBox);
            foreach (CardCase c in Object.FindObjectsByType<CardCase>(FindObjectsSortMode.None))
                HighlightTarget.Mark(c.gameObject, TutorialDefaults.Showcase);
            foreach (DecorReaction d in Object.FindObjectsByType<DecorReaction>(FindObjectsSortMode.None))
            {
                string key = d.Kind == UpgradeKind.Poster ? TutorialDefaults.DecorPoster
                    : d.Kind == UpgradeKind.Lamp ? TutorialDefaults.DecorLamp
                    : d.Kind == UpgradeKind.AirCon ? TutorialDefaults.DecorAirCon : null;
                if (key != null) HighlightTarget.Mark(d.gameObject, key);
            }
        }

        static void Mark<T>(string key) where T : Component
        {
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                HighlightTarget.Mark(c.gameObject, key);
        }

        // 빌더가 버튼마다 연결한 함수 (ShopSceneBuilder · CustomerFloorBuilder)
        static string KeyForButton(PokeButton button)
        {
            for (int i = 0; i < button.pressed.GetPersistentEventCount(); i++)
            {
                switch (button.pressed.GetPersistentMethodName(i))
                {
                    case "TryPay": return TutorialDefaults.PosPay;
                    case "TryClockOut": return TutorialDefaults.ClockOut;
                    case "TryOpenBox": return TutorialDefaults.BoxButton;
                    case "Sleep": return TutorialDefaults.BedButton;
                }
            }
            return null;
        }
    }
}
