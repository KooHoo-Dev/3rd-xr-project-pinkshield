using CardShop.Cards;

namespace CardShop.Logic
{
    public enum OpenFailReason
    {
        None = 0,
        NotEnoughMoney = 1,
        NoFreePack = 2,
    }

    /// <summary>
    /// 팩 구매·개봉 규칙 (설계서 Q3): 개봉을 시작하는 순간 차감, 그날 무료 팩이 남아 있으면 무료.
    /// 카드는 개봉 즉시 보유 목록(도감 포함)에 들어갑니다. 바인더·진열대는 이 데이터를 보여주는 뷰입니다.
    /// </summary>
    public sealed class PackOpeningService
    {
        readonly GameSessionProvider session;
        readonly PackRoller roller;
        readonly EconomyConfig economy;

        public PackOpeningService(GameSessionProvider session, PackRoller roller, EconomyConfig economy)
        {
            this.session = session;
            this.roller = roller;
            this.economy = economy;
        }

        public PackRoller Roller => roller;


        /// <summary>단일 팩. isGift = 사장님이 준 팩(1일차, 무료이며 무료 팩 횟수를 쓰지 않음).</summary>
        public bool TryOpenSingle(PackClue clue, bool isGift, out PackResult result, out OpenFailReason fail)
        {
            GameSession s = session();
            result = null;
            fail = OpenFailReason.None;

            // 선반 팩은 늘 유료. 오늘 무료 팩은 계산대 위 그 팩(TryOpenFree)으로만 엽니다 (11-19 플레이 피드백:
            // 예전에는 "다음에 사는 팩 1개가 0원"이라 계산대 무료 팩이 사라져 버그처럼 보였음)
            if (!isGift && !s.Wallet.TrySpend(economy.packPrice)) { fail = OpenFailReason.NotEnoughMoney; return false; }

            result = roller.Roll(clue, false);
            Register(s, result);
            return true;
        }

        /// <summary>오늘 무료 팩 (계산대 위, 영업 중 도둑에게서 지킨 팩). 남은 무료 팩이 없으면 실패.</summary>
        public bool TryOpenFree(out PackResult result, out OpenFailReason fail)
        {
            GameSession s = session();
            result = null;
            fail = OpenFailReason.None;
            if (s.FreePacksLeft <= 0) { fail = OpenFailReason.NoFreePack; return false; }
            s.FreePacksLeft--;

            result = roller.Roll(PackClue.None, false);
            Register(s, result);
            return true;
        }

        /// <summary>상자: 단서 없이 packsPerBox개를 한 번에 엽니다.</summary>
        public bool TryOpenBox(out PackResult[] results, out OpenFailReason fail)
        {
            GameSession s = session();
            results = null;
            fail = OpenFailReason.None;
            if (!s.Wallet.TrySpend(economy.boxPrice)) { fail = OpenFailReason.NotEnoughMoney; return false; }

            results = new PackResult[economy.packsPerBox];
            for (int i = 0; i < results.Length; i++)
            {
                results[i] = roller.Roll(PackClue.None, true);
                Register(s, results[i]);
            }
            return true;
        }

        static void Register(GameSession s, PackResult result)
        {
            result.IsNew = new bool[result.Cards.Length];
            for (int i = 0; i < result.Cards.Length; i++)
            {
                CardData card = result.Cards[i];
                result.IsNew[i] = s.Collection.Add(card.id);
                if (s.Stats.bestCard == null || card.basePrice > s.Stats.bestCard.basePrice)
                    s.Stats.bestCard = card;
            }

            DayLedger day = s.Ledger;
            day.packsOpened++;
            foreach (bool isNew in result.IsNew) if (isNew) day.newCards++;
            foreach (CardData card in result.Cards) if ((int)card.rarity > day.bestRarity) day.bestRarity = (int)card.rarity;
            if (result.Special == SpecialPackType.GodPack) day.godPack = true;
            if (result.Special == SpecialPackType.LegendaryPack) day.legendaryPack = true;

            s.Stats.packsOpened++;
            if (result.Special == SpecialPackType.GodPack) s.Stats.godPacks++;
            if (result.Special == SpecialPackType.LegendaryPack) s.Stats.legendaryPacks++;
        }
    }

    /// <summary>새 게임마다 GameSession이 바뀌므로 서비스는 현재 세션을 함수로 받습니다.</summary>
    public delegate GameSession GameSessionProvider();
}
