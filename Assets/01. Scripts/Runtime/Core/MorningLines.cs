using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Logic;

namespace CardShop.Core
{
    /// <summary>어제(전날) 카드팩 결과에 따른 아침 혼잣말의 "기분".</summary>
    public enum MorningMood
    {
        Plain = 0,      // 어제 최고가 노말·레어 (또는 1일차 직후 등 기록 없음)
        NoPacks = 1,    // 어제 팩을 하나도 안 열었음
        SuperRare = 2,  // 어제 최고 수퍼레어
        Unique = 3,     // 어제 최고 유니크
        Legendary = 4,  // 어제 레전더리 또는 레전더리 확정 팩
        LastDay = 5,    // 마지막 날 (다른 기분보다 우선)
        Endless = 6,    // 무한 모드 연장 근무 날 (어제 수퍼레어 이상이면 그 기분이 우선, J4)
        GodPack = 7,    // 어제 갓팩 (5장 모두 유니크 이상). 레전더리보다 우선
        DisplayOne = 10,  // 둘째 줄: 진열대에 내 카드 1장
        DisplayMany = 11, // 둘째 줄: 진열대에 내 카드 2장 이상
    }

    /// <summary>
    /// 잠에서 깬 아침 혼잣말 (시연 피드백: 전날 카드팩 결과에 따라 달라지게).
    /// 문장은 MorningLineSet(SO)에 붙여 넣은 목록을 쓰고, 비어 있는 기분은 아래 기본 문장을 씁니다.
    /// 순수 C#이라 테스트로 "기분 고르기 · 파싱 · 연속 일차 대사 겹침 방지"를 고정합니다.
    /// 1일차는 잠을 자고 오는 것이 아니라서 2일차부터 쓰입니다.
    /// </summary>
    public static class MorningLines
    {
        static readonly Dictionary<MorningMood, string[]> Defaults = new Dictionary<MorningMood, string[]>
        {
            [MorningMood.Plain] = new[]
            {
                "알바 {day}일차, 오늘은 좋은 거 나와라!",
                "어제 팩은 조용했지. 오늘은 내 차례야.",
                "알람 세 번 끄고 일어났다. 그래도 출근!",
                "손 씻고 가자. 팩 뜯기 전엔 경건하게.",
                "노말도 모으면 다 추억이지, 뭐.",
                "오늘은 왼쪽 팩을 골라 볼까? 그냥 기분상.",
                "바인더 빈칸이 나를 부르고 있어.",
                "커피 한 잔 마시고 오늘도 힘내자.",
            },
            [MorningMood.NoPacks] = new[]
            {
                "어제는 팩을 하나도 못 뜯었네… 오늘은 꼭!",
                "팩 뜯는 꿈을 꿨다. 이건 신호야.",
                "손이 근질근질하다. 오늘은 한 팩이라도!",
                "일만 하다 하루가 갔지. 오늘은 나를 위해서도.",
                "포장 비닐 찢는 소리가 그립다.",
                "돈 모으는 것도 좋지만, 설렘도 필요해.",
                "어제 아낀 운, 오늘 몰아서 쓰자.",
                "{day}일차. 진열대 팩들이 날 쳐다보는 것 같아.",
            },
            [MorningMood.SuperRare] = new[]
            {
                "어제 반짝이던 수퍼레어, 오늘은 유니크인가?",
                "그 반짝임, 한 번만 더 보고 싶다!",
                "수퍼레어 기운이 남아 있을 때 출근하자.",
                "어제 그 카드, 슬리브에 잘 넣어 뒀지?",
                "오늘도 반짝이는 거 하나만 부탁해.",
                "바인더 펼쳐 놓고 한참 봤다. 히히.",
                "손끝 감이 좋아. 오늘도 이어 가자!",
                "{day}일차, 기세가 왔어. 놓치지 말자.",
            },
            [MorningMood.Unique] = new[]
            {
                "어제 유니크라니, 아직도 꿈 같아…",
                "유니크의 여운이 손끝에 남아 있다.",
                "자기 전에 세 번은 꺼내 봤다. 진짜였어.",
                "그 카드 생각에 알람보다 먼저 깼어.",
                "게시판에 자랑 글 써도 되겠지?",
                "오늘은 침착하게. 어제로 충분해… 아마도.",
                "한 번 왔으니 또 올 거야. 그렇지?",
            },
            [MorningMood.Legendary] = new[]
            {
                "어제 레전더리… 꿈 아니지? 꼬집어 봐야지.",
                "어제 깠던 그 자리에서 다시 까볼까?",
                "사장님한테 말할까? 아니, 비밀로 하자.",
                "아직도 심장이 콩닥거린다.",
                "{day}일차. 어제 같은 날, 또 오게 만들 거야!",
                "탑로더에 고이 모셔 뒀다. 이제 출근하자.",
                "운을 다 썼을지도… 그래도 출근은 해야지.",
            },
            [MorningMood.GodPack] = new[]
            {
                "어제 갓팩… 다섯 장이 전부 빛났어.",
                "갓팩을 뜯은 손이 바로 금손이다.",
                "꿈에서도 다섯 장이 반짝였다.",
                "어제 그 팩 포장지, 버리지 말 걸 그랬나?",
                "갓팩 뜯은 알바생, 오늘도 출근합니다.",
                "커뮤니티에 갓팩 인증 글 올려야겠다.",
                "그런 팩이 진짜 있구나… 아직도 얼떨떨해.",
                "{day}일차. 어제의 나, 고마워!",
            },
            [MorningMood.LastDay] = new[]
            {
                "마지막 날이다. 레전더리 한 장만 부탁해!",
                "마지막 {day}일차. 후회 없이 가 보자!",
                "이 가게 문을 여는 것도 오늘이 마지막이네.",
                "사장님께 인사 제대로 하고 끝내야지.",
                "바인더 빈칸, 오늘 하나라도 더 채우자.",
                "마지막이니까 빗자루질도 깔끔하게!",
                "정들었네, 이 계산대.",
                "오늘은 아끼지 말고 끝까지 즐기자.",
            },
            // 연장 날에는 일차 숫자가 8, 9…라 어색해서 {day}를 쓰지 않음
            [MorningMood.Endless] = new[]
            {
                "오늘도 연장 근무다! 계약서는 끝났는데…",
                "이 정도면 거의 정직원 아닌가?",
                "사장님은 내가 안 그만둘 걸 아시는 걸까.",
                "연장 근무도 근무다. 좋은 거 나와라!",
                "사직서는 책상 위에 있다. 오늘은 아직 아니야.",
                "바인더가 다 찰 때까지는 못 그만둬.",
                "출근길이 이제 눈 감고도 보인다.",
                "단골 손님 얼굴이 하나둘 익숙해진다.",
            },
            // 둘째 줄: 어젯밤 집에서 판매 등록한 카드가 오늘 진열대에 있을 때
            [MorningMood.DisplayOne] = new[]
            {
                "어젯밤 진열한 카드, 오늘은 주인 만나라.",
                "진열대에 내 카드 한 장. 괜히 두근거린다.",
                "팔리면 좋겠다. 근데 조금 아쉽기도 하고.",
                "그 카드 앞에서 손님이 멈춰 주면 좋겠다.",
                "좋은 손님한테 가라, 내 카드야.",
            },
            [MorningMood.DisplayMany] = new[]
            {
                "진열대에 내 카드가 가득. 다 팔려라!",
                "내 카드들, 오늘 열심히 일해 줘.",
                "손님 눈에 하나라도 걸려라~",
                "진열대가 오늘 내 지갑에 돈을 채워 줄 거야.",
                "팔린 만큼 자산이 쌓인다. 힘내자!",
            },
        };

        /// <summary>
        /// 전날(prev) 기록으로 오늘 아침의 기분을 정합니다. prev가 없으면 Plain.
        /// 무한 모드(endless)에는 마지막 날이 없고, 어제 수퍼레어 이상이 아니면 연장 근무 기분입니다.
        /// </summary>
        public static MorningMood MoodFor(int day, int dayCount, DayLedger prev, bool endless = false)
        {
            if (!endless && dayCount > 1 && day >= dayCount) return MorningMood.LastDay;
            if (prev != null)
            {
                if (prev.godPack) return MorningMood.GodPack;
                if (prev.legendaryPack || prev.bestRarity >= (int)CardRarity.Legendary) return MorningMood.Legendary;
                if (prev.bestRarity >= (int)CardRarity.Unique) return MorningMood.Unique;
                if (prev.bestRarity >= (int)CardRarity.SuperRare) return MorningMood.SuperRare;
            }
            if (endless) return MorningMood.Endless;
            if (prev == null) return MorningMood.Plain;
            if (prev.packsOpened == 0) return MorningMood.NoPacks;
            return MorningMood.Plain;
        }

        /// <param name="custom">MorningLineSet에서 파싱한 문장들. 없거나 해당 기분이 비어 있으면 기본 문장.</param>
        /// <param name="onDisplay">지금 진열대에 올라 있는 내 카드 수 (어젯밤 집에서 등록). 1장 이상이면 진열 한 줄을 덧붙임</param>
        public static string For(int day, int dayCount, DayLedger prev, IReadOnlyDictionary<MorningMood, List<string>> custom = null, bool endless = false, int onDisplay = 0)
        {
            string line = Line(MoodFor(day, dayCount, prev, endless), day, custom);
            MorningMood? display = DisplayMoodFor(onDisplay);
            return display.HasValue ? line + "\n" + Line(display.Value, day, custom) : line;
        }

        /// <summary>진열 카드 수에 따른 둘째 줄. 진열이 비었으면 없음.</summary>
        public static MorningMood? DisplayMoodFor(int onDisplay) =>
            onDisplay <= 0 ? (MorningMood?)null : onDisplay == 1 ? MorningMood.DisplayOne : MorningMood.DisplayMany;

        static string Line(MorningMood mood, int day, IReadOnlyDictionary<MorningMood, List<string>> custom)
        {
            IReadOnlyList<string> pool = null;
            if (custom != null && custom.TryGetValue(mood, out List<string> list) && list.Count > 0) pool = list;
            if (pool == null) pool = Defaults[mood];

            // 일차를 돌려 쓰므로 같은 기분이 이어져도 연속 날짜에는 다른 문장이 나옴(문장이 2개 이상일 때)
            int index = day < 0 ? 0 : day % pool.Count;
            return pool[index].Replace("{day}", day.ToString());
        }

        /// <summary>
        /// 한 줄에 "기분|문장" 형식. 빈 줄과 '#'로 시작하는 줄은 무시. 기분 이름은 MorningMood 이름(대소문자 무시).
        /// 알 수 없는 기분이나 문장이 빈 줄은 건너뜁니다. 문장 안의 {day}는 일차 숫자로 바뀝니다.
        /// </summary>
        public static Dictionary<MorningMood, List<string>> Parse(string raw) => LineParser.Parse<MorningMood>(raw);
    }
}
