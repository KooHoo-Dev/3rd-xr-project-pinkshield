using System;
using System.Collections.Generic;

namespace CardShop.Logic
{
    /// <summary>
    /// 한 판의 모든 상태 (설계서 5장). 순수 C#이라 테스트에서 바로 만들 수 있습니다.
    /// 새 게임 = new GameSession(). 저장 기능은 없습니다.
    /// </summary>
    public sealed class GameSession
    {
        /// <summary>1부터 시작</summary>
        public int Day { get; private set; } = 1;
        public Wallet Wallet { get; }
        public Collection Collection { get; } = new Collection();
        public List<string> SalesDisplay { get; } = new List<string>();
        /// <summary>방 진열장에 세운 카드 (E1). 보유 목록에서 빠져 있어 팔 수 없고, 최종 점수에는 카드 가치로 들어갑니다 (ShowcaseRules).</summary>
        public List<string> Showcase { get; } = new List<string>();
        /// <summary>컴퓨터 3단계 중고 장터에 올린 카드 (없으면 null). 보유 목록에서 빠져 있음 (OnlineStallRules, DEC-040 U2)</summary>
        public string OnlineStall { get; set; }
        /// <summary>방 업그레이드 단계 · 택배 · 쓴 돈 (E2)</summary>
        public UpgradeState Upgrades { get; } = new UpgradeState();
        public DayLedger Ledger { get; private set; } = new DayLedger();
        /// <summary>어제의 장부 (아침 혼잣말이 어제 카드팩 결과를 보고 달라지도록). 1일차에는 null.</summary>
        public DayLedger Previous { get; private set; }
        public SessionStats Stats { get; } = new SessionStats();
        public int FreePacksLeft { get; set; }
        /// <summary>7일 뒤 계속하기 (무한모드_설계 J4)</summary>
        public EndlessState Endless { get; } = new EndlessState();

        public GameSession(int startingMoney) => Wallet = new Wallet(startingMoney);

        /// <summary>다음 날로 넘어가며 당일 장부를 비웁니다.</summary>
        public void AdvanceDay()
        {
            Day++;
            Previous = Ledger;
            Ledger = new DayLedger();
            Upgrades.AdvanceDay(); // 어젯밤 안 연 택배 상자는 아침에 저절로 열림 (DEC-040 U5)
        }
    }

    public sealed class Wallet
    {
        public int Money { get; private set; }

        public Wallet(int money) => Money = money;


        public void Add(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Set(Money + amount);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Money < amount) return false;
            Set(Money - amount);
            return true;
        }

        void Set(int value) => Money = value;
    }

    /// <summary>
    /// 보유 카드와 도감. 도감은 "한 번이라도 얻은 카드" 기준이라 팔아도 유지됩니다 (설계서 Q9).
    /// </summary>
    public sealed class Collection
    {
        readonly Dictionary<string, int> owned = new Dictionary<string, int>();
        readonly HashSet<string> discovered = new HashSet<string>();

        public int DiscoveredCount => discovered.Count;
        public IReadOnlyDictionary<string, int> Owned => owned;

        /// <summary>장수가 바뀔 때마다 (바뀐 카드 ID). 바인더 같은 뷰가 다시 그리는 신호입니다.</summary>
        public event Action<string> Changed;

        public int CountOf(string id) => owned.TryGetValue(id, out int n) ? n : 0;
        public bool IsDiscovered(string id) => discovered.Contains(id);

        /// <summary>카드 추가. 처음 얻은 카드면 true.</summary>
        public bool Add(string id)
        {
            owned[id] = CountOf(id) + 1;
            bool isNew = discovered.Add(id);
            Changed?.Invoke(id);
            return isNew;
        }

        public bool Remove(string id)
        {
            int n = CountOf(id);
            if (n == 0) return false;
            if (n == 1) owned.Remove(id); else owned[id] = n - 1;
            Changed?.Invoke(id);
            return true;
        }

        public int TotalCards
        {
            get { int sum = 0; foreach (int n in owned.Values) sum += n; return sum; }
        }
    }

    /// <summary>하루 정산 장부 (기획서 5-8 일별 정산 화면 내역).</summary>
    public sealed class DayLedger
    {
        public struct Sale
        {
            public string cardId;
            public int gross;      // 수수료 전
            public int net;        // 수령액
        }

        public int basePay;
        public int cleanupsDone;
        public int cleanupsMissed;
        public int customersServed;
        public int checkoutsFast;     // 기준 시간 안에 끝낸 계산 (EconomyRules.IsFastCheckout)
        public int customersLost;     // 줄에서 기다리다 떠난 손님
        public bool freePackStolen;   // 도둑을 놓쳐 오늘 무료 팩을 잃음 (ThiefRules.FreePacksToday)
        public int thievesCaught;     // 빗자루로 잡은 도둑
        // 아침 혼잣말용 하루 요약 (PackOpeningService가 개봉할 때마다 채움)
        public int packsOpened;
        public int newCards;
        public int bestRarity = -1;   // CardRarity 숫자, 아직 안 열었으면 -1
        public bool godPack;
        public bool legendaryPack;
        public readonly List<Sale> sales = new List<Sale>();
        // "가구 덕분에" 정산 줄 (DEC-040, UpgradeCredits)
        public int extraSlotSales;      // 바인더로 늘린 판매 칸에서 팔린 장수
        public int extraSlotNet;        // 그 수령액
        public int patienceSaved;       // 침대 덕분에 기다려 준 손님 (기본 인내 시간을 넘겨 계산)
        public string onlineSaleCard;   // 어젯밤 중고 장터에서 팔린 카드 이름 (없으면 null)
        public int onlineSaleNet;       // 그 수령액 (밤에 바로 지갑에 들어옴)
    }

    public sealed class SessionStats
    {
        public int packsOpened;
        public int godPacks;
        public int legendaryPacks;
        public CardShop.Cards.CardData bestCard; // 가장 비싼 카드 (최종 정산 화면)
    }
}
