using System;
using System.Collections.Generic;
using CardShop.Logic;

namespace CardShop.Core
{
    /// <summary>손님이 말하는 순간 (CustomerLineSet의 "상황" 이름과 같음).</summary>
    public enum CustomerMoment
    {
        Enter = 0,     // 가게에 들어옴
        Browse = 1,    // 진열대 구경
        Waiting = 2,   // 줄에서 기다리는 중 (인내 시간 waitingAt 지점)
        Impatient = 3, // 재촉 (인내 시간 impatientAt 지점) — 곧 떠난다는 신호
        Checkout = 4,  // 계산대에 물건을 올림
        PaidCard = 5,  // 판매 등록 카드를 삼
        Paid = 6,      // 가게 재고만 삼
        Angry = 7,     // 기다리다 떠남
    }

    /// <summary>
    /// 손님 말풍선 대사 고르기 (2026-11-18 기기 테스트 피드백). 문장은 CustomerLineSet(SO)에 붙여 넣은 목록을 쓰고,
    /// 비어 있는 상황은 아래 기본 문장을 씁니다. 같은 상황에서 바로 전에 나온 문장은 피합니다(문장이 2개 이상일 때).
    /// 순수 C#이라 테스트로 "파싱 · 빈 문장 없음 · 연속 중복 없음"을 고정합니다 (CustomerLinesTests).
    /// </summary>
    public sealed class CustomerLines
    {
        static readonly Dictionary<CustomerMoment, string[]> Defaults = new Dictionary<CustomerMoment, string[]>
        {
            [CustomerMoment.Enter] = new[]
            {
                "안녕하세요~",
                "여기 카드 맛집이라던데?",
                "오늘은 뭐가 들어왔으려나.",
                "구경 좀 할게요!",
                "와, 새 팩 냄새다.",
                "퇴근하자마자 달려왔어요!",
                "지난번에 왔던 그 가게 맞지?",
                "친구가 여기서 좋은 거 뽑았대요.",
                "오늘은 구경만… 진짜 구경만 할 거야.",
                "사장님 계세요? 아, 알바생이구나.",
            },
            [CustomerMoment.Browse] = new[]
            {
                "음… 이거 괜찮은데?",
                "이 카드 실물로 보니까 더 예쁘다.",
                "가격 실화야…?",
                "살까 말까 살까 말까…",
                "오, 이건 처음 보는 거다!",
                "홀로 반짝이는 것 좀 봐.",
                "엄마한테 혼나려나… 하나만 사자.",
                "내 바인더에 딱 빈칸인데.",
                "이건 덱에 넣으면 좋겠다.",
                "진열 깔끔하네, 보기 편하다.",
            },
            [CustomerMoment.Waiting] = new[]
            {
                "줄이 좀 기네…",
                "오늘 가게 북적이네요.",
                "천천히 하셔도 돼요~",
                "(폰 보는 중)",
                "앞사람은 뭐 샀으려나.",
                "집에 가서 어디서부터 뜯을까…",
                "(콧노래를 흥얼거린다)",
                "다음이 제 차례죠?",
            },
            [CustomerMoment.Impatient] = new[]
            {
                "저기요, 아직이에요?",
                "조금만 빨리 부탁드려요…",
                "버스 시간 다 됐는데…",
                "(발을 동동 구른다)",
                "학원 늦겠다…",
                "(한숨을 푹 쉰다)",
                "계산대가 하나 더 있으면 좋겠다…",
                "아직 멀었나요…?",
            },
            [CustomerMoment.Checkout] = new[]
            {
                "이거 계산해 주세요!",
                "이렇게 주세요~",
                "봉투에 담아 주세요.",
                "계산이요!",
                "카드로 할게요.",
                "이거요! 빨리 뜯어 보고 싶어요.",
                "이것만 주세요. 오늘은 참았어요.",
                "영수증은 괜찮아요.",
            },
            [CustomerMoment.PaidCard] = new[]
            {
                "{card}! 드디어 손에 넣었다!",
                "{card}, 이거 찾고 있었어요!",
                "와, {card}라니… 감사합니다!",
                "{card}로 덱 완성이다!",
                "친구한테 {card} 자랑해야지.",
                "{card}, 상태도 완벽하네요!",
            },
            [CustomerMoment.Paid] = new[]
            {
                "감사합니다~",
                "수고하세요!",
                "또 올게요!",
                "좋은 거 나오면 좋겠다~",
                "집 가서 바로 뜯어 봐야지.",
                "다음에 신상 들어오면 또 올게요.",
                "여기 분위기 좋네요.",
                "좋은 하루 보내세요!",
            },
            [CustomerMoment.Angry] = new[]
            {
                "됐어요, 다음에 올게요.",
                "너무 오래 걸리네… 그냥 갈래요.",
                "에잇, 딴 데 가야지.",
                "시간이 없어서요, 죄송해요.",
                "줄이 안 줄어드네… 포기.",
                "다음엔 좀 빨리 해 주세요.",
            },
        };

        readonly Dictionary<CustomerMoment, List<string>> custom;
        readonly IRandomSource random;
        readonly Dictionary<CustomerMoment, int> lastIndex = new Dictionary<CustomerMoment, int>();

        /// <param name="raw">CustomerLineSet.lines (없으면 기본 문장만)</param>
        public CustomerLines(string raw, IRandomSource random)
        {
            custom = Parse(raw);
            this.random = random ?? new SystemRandomSource();
        }

        /// <summary>상황에 맞는 문장 하나. {card}는 cardName으로 바꿉니다(없으면 "이 카드").</summary>
        public string Pick(CustomerMoment moment, string cardName = null)
        {
            IReadOnlyList<string> pool = Pool(moment);
            int index = random.Range(0, pool.Count);
            // 바로 전 문장 피하기: 같은 번호가 나오면 다음 번호로
            if (pool.Count > 1 && lastIndex.TryGetValue(moment, out int last) && index == last) index = (index + 1) % pool.Count;
            lastIndex[moment] = index;
            return pool[index].Replace("{card}", string.IsNullOrEmpty(cardName) ? "이 카드" : cardName);
        }

        IReadOnlyList<string> Pool(CustomerMoment moment)
        {
            if (custom.TryGetValue(moment, out List<string> list) && list.Count > 0) return list;
            return Defaults[moment];
        }

        /// <summary>
        /// 한 줄에 "상황|문장". 빈 줄과 '#'로 시작하는 줄은 무시. 상황 이름은 CustomerMoment 이름(대소문자 무시).
        /// 알 수 없는 상황이나 문장이 빈 줄은 건너뜁니다.
        /// </summary>
        public static Dictionary<CustomerMoment, List<string>> Parse(string raw) => LineParser.Parse<CustomerMoment>(raw);
    }
}
