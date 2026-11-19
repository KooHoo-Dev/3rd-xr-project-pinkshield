using CardShop.Cards;
using CardShop.Core;
using CardShop.Presentation;
using TMPro;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 집 업그레이드(E1 진열장 · E2 방 업그레이드, 집_업그레이드_설계)를 씬에 붙입니다. 씬을 다시 빌드하지 않아도 되게
    /// 씬이 열리면 스스로 실행됩니다(TutorialDirector.Install과 같은 방식). 이미 붙어 있으면 아무것도 하지 않습니다.
    /// - 바인더 오른쪽: 진열장 패널 (ShowcasePanel)
    /// - 책장 앞: 카드 진열대 (ShowcaseShelf)
    /// - 컴퓨터 옆 오른쪽 벽: 슬라임마켓 화면 (MarketScreen)
    /// - 방 가구 · 택배 상자: RoomUpgradeView
    /// </summary>
    public static class HomeUpgradeInstaller
    {
        // 슬라임마켓 화면: 오른쪽 벽(컴퓨터 책상 옆), 방 안(-X)을 봄. 집 실내 로컬 좌표
        static readonly Vector3 MarketPosition = new Vector3(ShopLayout.HomeMaxX - ShopLayout.HomeWallHalf - 0.02f, 1.45f, 0.45f);
        const float MarketYaw = -90f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (GameContext.Instance == null || Object.FindAnyObjectByType<ShowcasePanel>(FindObjectsInactive.Include) != null) return;
            Transform interior = HomeRoom.Interior();
            BinderView binder = Object.FindAnyObjectByType<BinderView>(FindObjectsInactive.Include);
            if (interior == null || binder == null)
            {
                Debug.LogWarning("[HomeUpgradeInstaller] 집 실내(HomeInterior) 또는 바인더가 없어 진열장 · 슬라임마켓을 붙이지 못했습니다.");
                return;
            }

            UpgradeCatalog catalog = GameContext.Instance.Upgrades.Catalog;
            TMP_FontAsset font = WorldUiKit.FindFont();

            ShowcasePanel.Create(binder, font, GameContext.Instance.Upgrades.MaxShowcaseSlots());
            ShowcaseShelf.Create(interior, catalog);
            // 화면은 집 페이즈 이벤트를 받아야 하므로 늘 켜져 있는 Home 루트 아래 (집 실내는 집에서만 켜짐 — ZoneVisibility)
            // 슬라임마켓은 컴퓨터 모니터의 두 번째 쪽으로 (11-20 J1). 모니터(커뮤니티 화면)가 없는 씬이면 예전처럼 오른쪽 벽
            if (!HomeComputer.Create(catalog, font))
            {
                Vector3 local = interior.parent.InverseTransformPoint(interior.TransformPoint(MarketPosition));
                MarketScreen.Create(interior.parent, local, MarketYaw, catalog, font);
            }
            RoomUpgradeView.Create(interior, catalog, font, binder);

            FinalBoard final = Object.FindAnyObjectByType<FinalBoard>(FindObjectsInactive.Include);
            if (final != null) FinalShowcaseStrip.Create(final, binder.Styles, font);
        }
    }
}
