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
        Legendary = 4,  // 어제 레전더리 또는 갓팩 · 레전더리 확정 팩
        LastDay = 5,    // 마지막 날 (다른 기분보다 우선)
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
                "알바 {day}일차, 카드팩 좋은 거 나와라!",
                "{day}일차. 어제는 평범했으니 오늘은 뭔가 터지겠지?",
                "어제 팩은 그냥 그랬어… 오늘은 다를 거야!",
                "{day}일차 출근. 운은 꾸준히 쌓이는 거라고 했어.",
            },
            [MorningMood.NoPacks] = new[]
            {
                "어제는 팩을 하나도 안 열었네… 오늘은 꼭 열어 보자!",
                "{day}일차. 팩이 눈앞에 아른거려서 잠을 설쳤다.",
            },
            [MorningMood.SuperRare] = new[]
            {
                "어제 수퍼레어 봤지? 오늘도 그 기운 이어 가자!",
                "{day}일차, 어제 반짝이던 그 느낌이 아직 남아 있어.",
            },
            [MorningMood.Unique] = new[]
            {
                "어제 유니크가 나왔다니, 아직도 꿈 같아… 오늘도 가 보자!",
                "{day}일차. 유니크의 여운이 아직 손끝에 남아 있다.",
            },
            [MorningMood.Legendary] = new[]
            {
                "어제 레전더리가… 꿈 아니지? 오늘은 평정심, 평정심.",
                "{day}일차. 어제 같은 날이 또 올까? 아니, 오게 만들 거야!",
            },
            [MorningMood.LastDay] = new[]
            {
                "마지막 날이다. 오늘 레전더리 한 장만 나오면 소원이 없겠다!",
                "마지막 {day}일차. 후회 없이 끝까지 가 보자!",
            },
        };

        /// <summary>전날(prev) 기록으로 오늘 아침의 기분을 정합니다. prev가 없으면 Plain.</summary>
        public static MorningMood MoodFor(int day, int dayCount, DayLedger prev)
        {
            if (dayCount > 1 && day >= dayCount) return MorningMood.LastDay;
            if (prev == null) return MorningMood.Plain;
            if (prev.godPack || prev.legendaryPack || prev.bestRarity >= (int)CardRarity.Legendary) return MorningMood.Legendary;
            if (prev.bestRarity >= (int)CardRarity.Unique) return MorningMood.Unique;
            if (prev.bestRarity >= (int)CardRarity.SuperRare) return MorningMood.SuperRare;
            if (prev.packsOpened == 0) return MorningMood.NoPacks;
            return MorningMood.Plain;
        }

        /// <param name="custom">MorningLineSet에서 파싱한 문장들. 없거나 해당 기분이 비어 있으면 기본 문장.</param>
        public static string For(int day, int dayCount, DayLedger prev, IReadOnlyDictionary<MorningMood, List<string>> custom = null)
        {
            MorningMood mood = MoodFor(day, dayCount, prev);
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
        public static Dictionary<MorningMood, List<string>> Parse(string raw)
        {
            var result = new Dictionary<MorningMood, List<string>>();
            if (string.IsNullOrEmpty(raw)) return result;
            foreach (string rawLine in raw.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int bar = line.IndexOf('|');
                if (bar <= 0) continue;
                string moodName = line.Substring(0, bar).Trim();
                string text = line.Substring(bar + 1).Trim();
                if (text.Length == 0 || !System.Enum.TryParse(moodName, true, out MorningMood mood)) continue;
                if (!result.TryGetValue(mood, out List<string> list)) result[mood] = list = new List<string>();
                list.Add(text);
            }
            return result;
        }
    }
}
