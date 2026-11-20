using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>사기 시도의 결과 (거부 이유를 상점 화면에 알리기 위함).</summary>
    public enum BuyResult
    {
        Ok,
        Unknown,
        MaxLevel,
        /// <summary>이미 주문했거나 택배 상자가 와 있음 (한 분야에 한 번에 하나)</summary>
        Pending,
        NotEnoughMoney,
    }

    /// <summary>
    /// 방 업그레이드 판단 (집_업그레이드_설계 3장, 순수 C# · UpgradeRulesTests).
    /// 살 수 있나 · 다음 단계 가격 · 단계별 효과 값을 UpgradeCatalog와 GameSession.Upgrades로 계산합니다.
    /// 효과는 상자를 연 단계(UpgradeState.LevelOf) 기준입니다.
    /// </summary>
    public sealed class UpgradeRules
    {
        readonly UpgradeCatalog catalog;

        public UpgradeRules(UpgradeCatalog catalog) => this.catalog = catalog;

        public UpgradeCatalog Catalog => catalog;

        /// <summary>효과 없는 꾸미기 물건 (포스터 · 러그 · 조명 · 피규어)</summary>
        public static bool IsDecor(UpgradeKind kind) => kind >= UpgradeKind.Poster;

        /// <summary>마지막 단계 (처음 1 + 살 수 있는 단계 수)</summary>
        public int MaxLevel(UpgradeKind kind)
        {
            UpgradeCatalog.Track t = catalog.Get(kind);
            return t != null ? 1 + t.levels.Length : 1;
        }

        /// <summary>다음에 살 단계의 구매 정보. 최고 단계 · 주문 중이면 null.</summary>
        public UpgradeCatalog.Level NextLevel(UpgradeState state, UpgradeKind kind)
        {
            UpgradeCatalog.Track t = catalog.Get(kind);
            if (t == null || state.StatusOf(kind) != UpgradeStatus.None) return null;
            int index = state.LevelOf(kind) - 1; // 1단계면 levels[0](2단계 구매)
            return index >= 0 && index < t.levels.Length ? t.levels[index] : null;
        }

        public BuyResult Check(GameSession s, UpgradeKind kind)
        {
            UpgradeCatalog.Track t = catalog.Get(kind);
            if (t == null) return BuyResult.Unknown;
            if (s.Upgrades.StatusOf(kind) != UpgradeStatus.None) return BuyResult.Pending;
            if (s.Upgrades.LevelOf(kind) >= MaxLevel(kind)) return BuyResult.MaxLevel;
            UpgradeCatalog.Level next = NextLevel(s.Upgrades, kind);
            if (next == null) return BuyResult.MaxLevel;
            return s.Wallet.Money >= next.price ? BuyResult.Ok : BuyResult.NotEnoughMoney;
        }

        /// <summary>사기: 지갑에서 빼고 주문으로 기록합니다(다음 날 아침 택배).</summary>
        public BuyResult TryBuy(GameSession s, UpgradeKind kind, out int price)
        {
            price = 0;
            BuyResult r = Check(s, kind);
            if (r != BuyResult.Ok) return r;
            price = NextLevel(s.Upgrades, kind).price;
            if (!s.Wallet.TrySpend(price)) { price = 0; return BuyResult.NotEnoughMoney; }
            s.Upgrades.Order(kind, price);
            return BuyResult.Ok;
        }

        // ------------------------------------------------------------------ 효과 (단계별 값)

        public int ShowcaseSlots(UpgradeState st) =>
            UpgradeCatalog.At(catalog.showcaseSlots, st.LevelOf(UpgradeKind.Showcase), ShowcaseRules.BaseSlots);

        /// <summary>진열장 칸이 가장 많은 단계의 칸 수 (진열장 · 목록 버튼을 미리 만들어 둘 개수)</summary>
        public int MaxShowcaseSlots()
        {
            int max = ShowcaseRules.BaseSlots;
            if (catalog.showcaseSlots != null) foreach (int n in catalog.showcaseSlots) if (n > max) max = n;
            return max;
        }

        public bool ShowcaseLight(UpgradeState st) => st.LevelOf(UpgradeKind.Showcase) >= catalog.showcaseLightLevel;

        public int ExtraSalesSlots(UpgradeState st) => UpgradeCatalog.At(catalog.extraSalesSlots, st.LevelOf(UpgradeKind.Binder), 0);

        public float InstantSellBonus(UpgradeState st) => UpgradeCatalog.At(catalog.instantSellBonus, st.LevelOf(UpgradeKind.Binder), 0f);

        public bool GoldBinder(UpgradeState st) => st.LevelOf(UpgradeKind.Binder) >= catalog.goldBinderLevel;

        /// <summary>커뮤니티 화면이 며칠 뒤까지 인기 속성을 보여 주나 (1 = 내일)</summary>
        public int ForecastDays(UpgradeState st) => UpgradeCatalog.At(catalog.forecastDays, st.LevelOf(UpgradeKind.Computer), 1);

        public bool HasPriceList(UpgradeState st) => st.LevelOf(UpgradeKind.Computer) >= catalog.priceListLevel;

        /// <summary>손님 인내 시간 배율 (1.1 = +10%)</summary>
        public float PatienceMultiplier(UpgradeState st) => 1f + UpgradeCatalog.At(catalog.patienceBonus, st.LevelOf(UpgradeKind.Bed), 0f);

        public int ExtraFreePacks(UpgradeState st) =>
            UpgradeCatalog.At(catalog.extraFreePacks, st.LevelOf(UpgradeKind.Bed), 0)
            + (AllDecorOwned(st) ? CozyRoomFreePacks : 0); // 꾸미기 5종 "아늑한 방" (DEC-040 U3)

        /// <summary>꾸미기를 모두 갖추면 매일 더 받는 무료 팩</summary>
        public const int CozyRoomFreePacks = 1;

        /// <summary>최종 점수의 가구 값 = 산 가격 100% (DEC-031 Q2)</summary>
        public static int FurnitureValue(UpgradeState st) => st.Spent;

        /// <summary>산 가구 단계 수 (모든 분야의 단계 − 1 합, 꾸미기 1개 = 1). 방 점수 · 방 도감용 (DEC-040 U4)</summary>
        public static int FurniturePieces(UpgradeState st)
        {
            int n = 0;
            foreach (UpgradeKind k in (UpgradeKind[])System.Enum.GetValues(typeof(UpgradeKind))) n += st.LevelOf(k) - 1;
            return n;
        }

        /// <summary>살 수 있는 가구 단계 전체 수 (카탈로그 기준, "방 꾸미기 n / 전체")</summary>
        public int TotalPieces()
        {
            int n = 0;
            if (catalog.tracks != null) foreach (UpgradeCatalog.Track t in catalog.tracks) if (t != null && t.levels != null) n += t.levels.Length;
            return n;
        }

        /// <summary>꾸미기를 모두 갖췄나 ("아늑한 방", DEC-040 U3)</summary>
        public bool AllDecorOwned(UpgradeState st)
        {
            bool any = false;
            if (catalog.tracks != null)
                foreach (UpgradeCatalog.Track t in catalog.tracks)
                {
                    if (t == null || !IsDecor(t.kind)) continue;
                    any = true;
                    if (st.LevelOf(t.kind) < MaxLevel(t.kind)) return false;
                }
            return any;
        }
    }
}
