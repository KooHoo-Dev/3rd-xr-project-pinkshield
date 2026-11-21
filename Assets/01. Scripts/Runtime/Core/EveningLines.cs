using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Logic;

namespace CardShop.Core
{
    /// <summary>퇴근 혼잣말의 상황. Work* = 오늘 일한 모습, Pack* = 오늘 카드팩 결과. 한 줄씩 골라 두 줄로 말합니다.</summary>
    public enum EveningMood
    {
        WorkPlain = 0,     // 평범한 하루
        WorkBusy = 1,      // 손님을 많이 받음
        WorkClean = 2,     // 청소를 하나도 놓치지 않음
        WorkMessy = 3,     // 못 치운 청소가 있음
        WorkLost = 4,      // 기다리다 떠난 손님이 있음
        WorkQuiet = 5,     // 손님이 적었던 한가한 하루
        WorkThief = 6,     // 도둑을 잡음
        WorkRobbed = 7,    // 도둑을 놓쳐 무료 팩을 잃음
        PackNone = 10,     // 팩을 안 열었음
        PackPlain = 11,    // 노말 · 레어뿐
        PackNew = 12,      // 새 카드를 많이 모음
        PackSuperRare = 13,
        PackUnique = 14,
        PackLegendary = 15, // 레전더리 · 레전더리 확정 팩
        PackGodPack = 16,  // 갓팩 (5장 모두 유니크 이상). 레전더리보다 우선
        MoneyBig = 20,     // 진열 카드를 팔아 크게 범 (BigSaleNet 이상)
        MoneySold = 21,    // 진열 카드가 팔림
        MoneyUnsold = 22,  // 진열했지만 하나도 안 팔림
    }

    /// <summary>
    /// 퇴근 혼잣말 (4차 기기 피드백: 퇴근 메시지가 한 문장뿐이라 단조로웠음). 아침 혼잣말(MorningLines)과 같은 구조.
    /// 오늘 장부(DayLedger)로 "일한 모습 한 줄 + 카드팩 결과 한 줄"을 고릅니다.
    /// 문장은 EveningLineSet(SO)에 "상황|문장"으로 붙여 넣고, 비어 있는 상황은 아래 기본 문장을 씁니다.
    /// 문장 안의 {customers} · {cleanups} · {new} · {packs}는 오늘 숫자로 바뀝니다 (손님 수는 기본 문장에서 쓰지 않고 바쁨 · 한가로 표현).
    /// </summary>
    public static class EveningLines
    {
        /// <summary>이 수 이상 손님을 받으면 "바빴던 하루" (하루 예정 손님이 5~9명이라 8 이상)</summary>
        public const int BusyCustomers = 8;
        /// <summary>이 수 이하 손님을 받으면 "한가한 하루"</summary>
        public const int QuietCustomers = 5;
        /// <summary>새 카드를 이 수 이상 모으면 "수집이 잘 된 하루" (수퍼레어 이상이 없을 때)</summary>
        public const int ManyNewCards = 5;
        /// <summary>오늘 판매 수령액 합이 이 이상이면 "크게 번 날" (카드팩 10개 값, 유니크 1장쯤)</summary>
        public const int BigSaleNet = 100;

        static readonly Dictionary<EveningMood, string[]> Defaults = new Dictionary<EveningMood, string[]>
        {
            // 손님 수는 숫자로 말하지 않고 바쁨 · 한가로 표현 (11-20 피드백, 98. Doc/대사_다양화_분석.md)
            [EveningMood.WorkPlain] = new[]
            {
                "오늘도 무사히 알바 끝!",
                "무난한 하루였다. 이런 날도 좋지.",
                "가게 문 잠그고 나니 다리가 풀리네.",
                "큰일 없이 마감. 이게 제일 어려운 거야.",
                "오늘 하루도 계산대 지키기 성공.",
                "적당히 바쁘고 적당히 쉬었다.",
            },
            [EveningMood.WorkBusy] = new[]
            {
                "계산하느라 손이 안 보였다.",
                "오늘 가게 문이 닳는 줄 알았네.",
                "눈코 뜰 새 없었다. 물 한 모금도 못 마셨어.",
                "계산대에서 발이 안 떨어진 하루.",
                "숨 돌릴 틈이 없었다. 그래도 뿌듯해.",
                "가게가 하루 종일 북적북적했다.",
            },
            [EveningMood.WorkQuiet] = new[]
            {
                "오늘은 가게가 한산했다.",
                "조용한 하루. 진열대만 몇 번이고 정리했네.",
                "시계만 쳐다본 하루였다.",
                "손님 발길이 뜸했다. 내일은 북적이겠지?",
                "한가해서 카드 시세 공부나 했다.",
                "쉬어 가는 날도 있어야지.",
            },
            [EveningMood.WorkClean] = new[]
            {
                "오늘 청소 완벽. 가게가 반짝반짝!",
                "바닥에 쓰레기 하나 없이 마감. 사장님이 좋아하겠지.",
                "빗자루랑 한 몸이 된 하루.",
                "깨끗한 가게라 손님들도 기분 좋았겠지.",
            },
            [EveningMood.WorkMessy] = new[]
            {
                "청소를 좀 놓쳤네… 내일은 더 꼼꼼히.",
                "바닥에 뭐가 남아 있던 것 같은데… 못 본 걸로 하자.",
                "사장님 오시기 전에 치웠어야 했는데.",
                "그 얼룩, 꿈에 나올 것 같다.",
            },
            [EveningMood.WorkLost] = new[]
            {
                "기다리다 그냥 간 손님이 자꾸 생각난다.",
                "줄이 길어서 손님을 놓쳤다. 계산을 더 빨리 해야지.",
                "\"다음에 올게요\"가 귀에 맴돈다.",
                "손이 두 개뿐이라 아쉬운 날이었다.",
            },
            [EveningMood.WorkThief] = new[]
            {
                "도둑을 잡았다! 빗자루 실력 늘었네.",
                "오늘의 나, 슬라임 방범대원으로 인정.",
                "복면 쓰고 오면 모를 줄 알았나 보지?",
                "한 대 더 휘둘렀어야 했는데, 까비!",
            },
            [EveningMood.WorkRobbed] = new[]
            {
                "도둑한테 오늘 팩을 뺏겼다… 분하다.",
                "그 복면, 다음엔 꼭 잡는다.",
                "빗자루를 좀 더 빨리 들었어야 했는데.",
                "내 공짜 팩… 레전더리 있었던 거 아니야?",
            },
            // 돈 줄: 판매 등록한 카드가 팔렸는지. 금액은 정산표가 보여 주므로 기분만 (최종 자산 엔딩과 연결)
            [EveningMood.MoneyBig] = new[]
            {
                "오늘 판 카드 값이면 치킨 각이다!",
                "통장이 두둑해졌다. 발걸음이 가볍네.",
                "역시 슬라임 카드가 미래다!",
                "이 기세면 마지막엔 부자 알바생이겠는데?",
                "알바비보다 카드 판 돈이 더 많네…?",
                "오늘은 지갑이 웃는 날.",
            },
            [EveningMood.MoneySold] = new[]
            {
                "내 카드가 새 주인을 만났다. 잘 지내렴.",
                "진열한 카드가 팔렸다! 정산이 기다려진다.",
                "조금씩 쌓이는 게 자산이지.",
                "카드 판 돈, 어디에 쓸까 고민되네.",
                "좋은 손님이 데려갔으니 됐어.",
                "통장에 한 줄 더 찍히겠다.",
            },
            [EveningMood.MoneyUnsold] = new[]
            {
                "진열한 카드는 오늘도 그대로네.",
                "내 카드, 아직 주인을 못 만났구나.",
                "가격이 너무 비쌌나…?",
                "내일은 누가 데려가겠지.",
                "팔리면 아쉽고, 안 팔리면 서운하고.",
            },
            [EveningMood.PackNone] = new[]
            {
                "오늘은 팩을 하나도 못 뜯었네. 손이 근질근질하다.",
                "카드팩 냄새도 못 맡은 하루…",
                "오늘은 꾹 참았다. 운은 저금해 두자.",
                "팩 없는 하루는 조금 심심하네.",
            },
            [EveningMood.PackPlain] = new[]
            {
                "팩은 별거 없었다. 내일은 터지겠지.",
                "오늘 팩은 조용했다. 운은 아껴 두는 거야.",
                "노말도 모으면 추억이다… 그렇게 믿자.",
                "다 아는 얼굴들만 나왔네.",
                "반짝임은 없었지만 뜯는 맛은 있었다.",
            },
            [EveningMood.PackNew] = new[]
            {
                "새 카드가 {new}장이나 늘었다. 바인더 채우는 맛!",
                "도감 빈칸이 쑥쑥 줄어든다.",
                "처음 보는 카드가 많았던 날. 정리할 게 많네.",
                "집에 가서 바인더부터 펼쳐야지.",
            },
            [EveningMood.PackSuperRare] = new[]
            {
                "수퍼레어가 반짝이던 순간, 아직 눈에 선하다.",
                "오늘 수퍼레어 하나 건졌다. 나쁘지 않아!",
                "반짝이는 거 하나면 하루가 행복해.",
                "슬리브부터 씌워 줘야겠다.",
            },
            [EveningMood.PackUnique] = new[]
            {
                "유니크라니… 손이 아직도 떨린다.",
                "오늘 유니크 뽑은 거, 게시판에 자랑해도 되겠지?",
                "포장 뜯을 때 느낌이 달랐다니까.",
                "오늘 밤은 바인더 펼쳐 놓고 자야지.",
            },
            [EveningMood.PackGodPack] = new[]
            {
                "갓팩이다! 다섯 장이 전부 반짝였어!",
                "오늘 뜯은 그 팩, 평생 기억할 거야.",
                "갓팩이라니… 다리가 후들거린다.",
                "팩 하나로 오늘 하루 레전드!",
                "바인더 한 페이지가 통째로 빛나겠네.",
                "손이 떨려서 인증 사진도 흔들렸다.",
            },
            [EveningMood.PackLegendary] = new[]
            {
                "레전더리를 뽑은 날이다. 오늘은 잠이 안 오겠다.",
                "꿈 아니지? 오늘 정말 별이 떴다!",
                "손이 떨려서 계산을 어떻게 했나 몰라.",
                "이 순간을 위해 알바했나 봐.",
            },
        };

        /// <summary>오늘 일한 모습. 드문 일(도둑)이 먼저, 그다음 아쉬운 일(떠난 손님 · 못 한 청소), 바쁨 · 한가 · 깨끗함.</summary>
        public static EveningMood WorkMoodFor(DayLedger today)
        {
            if (today == null) return EveningMood.WorkPlain;
            if (today.freePackStolen) return EveningMood.WorkRobbed;
            if (today.thievesCaught > 0) return EveningMood.WorkThief;
            if (today.customersLost > 0) return EveningMood.WorkLost;
            if (today.cleanupsMissed > 0) return EveningMood.WorkMessy;
            if (today.customersServed >= BusyCustomers) return EveningMood.WorkBusy;
            if (today.customersServed <= QuietCustomers) return EveningMood.WorkQuiet;
            if (today.cleanupsDone > 0) return EveningMood.WorkClean;
            return EveningMood.WorkPlain;
        }

        /// <summary>오늘 카드팩 결과. 높은 등급이 먼저.</summary>
        public static EveningMood PackMoodFor(DayLedger today)
        {
            if (today == null || today.packsOpened == 0) return EveningMood.PackNone;
            if (today.godPack) return EveningMood.PackGodPack;
            if (today.legendaryPack || today.bestRarity >= (int)CardRarity.Legendary) return EveningMood.PackLegendary;
            if (today.bestRarity >= (int)CardRarity.Unique) return EveningMood.PackUnique;
            if (today.bestRarity >= (int)CardRarity.SuperRare) return EveningMood.PackSuperRare;
            if (today.newCards >= ManyNewCards) return EveningMood.PackNew;
            return EveningMood.PackPlain;
        }

        /// <summary>오늘 판매 결과. 판매도 없고 진열대도 비었으면 null (돈 줄을 말하지 않음).</summary>
        /// <param name="onDisplay">퇴근할 때 진열대에 남은 내 카드 수 (안 팔린 카드)</param>
        public static EveningMood? MoneyMoodFor(DayLedger today, int onDisplay)
        {
            int net = 0;
            if (today != null) foreach (DayLedger.Sale s in today.sales) net += s.net;
            if (today != null && today.sales.Count > 0) return net >= BigSaleNet ? EveningMood.MoneyBig : EveningMood.MoneySold;
            return onDisplay > 0 ? EveningMood.MoneyUnsold : (EveningMood?)null;
        }

        /// <summary>혼잣말: 일 한 줄 + (판매가 있었거나 진열 중이면) 돈 한 줄 + 카드 한 줄.</summary>
        /// <param name="custom">EveningLineSet에서 파싱한 문장들. 없거나 해당 상황이 비어 있으면 기본 문장.</param>
        /// <param name="onDisplay">퇴근할 때 진열대에 남은 내 카드 수</param>
        public static string For(int day, DayLedger today, IReadOnlyDictionary<EveningMood, List<string>> custom = null, int onDisplay = 0)
        {
            string work = Line(WorkMoodFor(today), day, today, custom);
            string pack = Line(PackMoodFor(today), day, today, custom);
            EveningMood? money = MoneyMoodFor(today, onDisplay);
            return money.HasValue
                ? work + "\n" + Line(money.Value, day, today, custom) + "\n" + pack
                : work + "\n" + pack;
        }

        static string Line(EveningMood mood, int day, DayLedger today, IReadOnlyDictionary<EveningMood, List<string>> custom)
        {
            IReadOnlyList<string> pool = null;
            if (custom != null && custom.TryGetValue(mood, out List<string> list) && list.Count > 0) pool = list;
            if (pool == null) pool = Defaults[mood];
            // 일차를 돌려 쓰므로 같은 상황이 이어져도 연속 날짜에는 다른 문장 (문장이 2개 이상일 때)
            string line = pool[day < 0 ? 0 : day % pool.Count];
            return line
                .Replace("{customers}", (today?.customersServed ?? 0).ToString())
                .Replace("{cleanups}", (today?.cleanupsDone ?? 0).ToString())
                .Replace("{new}", (today?.newCards ?? 0).ToString())
                .Replace("{packs}", (today?.packsOpened ?? 0).ToString())
                .Replace("{day}", day.ToString());
        }

        /// <summary>
        /// 한 줄에 "상황|문장" 형식. 빈 줄과 '#'로 시작하는 줄은 무시. 상황 이름은 EveningMood 이름(대소문자 무시).
        /// </summary>
        public static Dictionary<EveningMood, List<string>> Parse(string raw) => LineParser.Parse<EveningMood>(raw);
    }
}
