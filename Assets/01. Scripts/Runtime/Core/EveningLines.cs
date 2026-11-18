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
        PackNone = 10,     // 팩을 안 열었음
        PackPlain = 11,    // 노말 · 레어뿐
        PackNew = 12,      // 새 카드를 많이 모음
        PackSuperRare = 13,
        PackUnique = 14,
        PackLegendary = 15, // 레전더리 · 갓팩 · 레전더리 확정 팩
    }

    /// <summary>
    /// 퇴근 혼잣말 (4차 기기 피드백: 퇴근 메시지가 한 문장뿐이라 단조로웠음). 아침 혼잣말(MorningLines)과 같은 구조.
    /// 오늘 장부(DayLedger)로 "일한 모습 한 줄 + 카드팩 결과 한 줄"을 고릅니다.
    /// 문장은 EveningLineSet(SO)에 "상황|문장"으로 붙여 넣고, 비어 있는 상황은 아래 기본 문장을 씁니다.
    /// 문장 안의 {customers} · {cleanups} · {new} · {packs}는 오늘 숫자로 바뀝니다.
    /// </summary>
    public static class EveningLines
    {
        /// <summary>이 수 이상 손님을 받으면 "바빴던 하루"</summary>
        public const int BusyCustomers = 5;
        /// <summary>새 카드를 이 수 이상 모으면 "수집이 잘 된 하루" (수퍼레어 이상이 없을 때)</summary>
        public const int ManyNewCards = 5;

        static readonly Dictionary<EveningMood, string[]> Defaults = new Dictionary<EveningMood, string[]>
        {
            [EveningMood.WorkPlain] = new[]
            {
                "오늘도 무사히 알바 끝!",
                "손님 {customers}명. 무난한 하루였다.",
                "가게 문 잠그고 나니 다리가 풀리네.",
            },
            [EveningMood.WorkBusy] = new[]
            {
                "손님 {customers}명… 계산하느라 손이 안 보였다.",
                "오늘 가게 불났다, 손님이 {customers}명이나!",
            },
            [EveningMood.WorkClean] = new[]
            {
                "청소 {cleanups}번 다 해치웠다. 가게가 반짝반짝!",
                "바닥에 쓰레기 하나 없이 마감. 사장님이 좋아하겠지.",
            },
            [EveningMood.WorkMessy] = new[]
            {
                "청소를 좀 놓쳤네… 내일은 더 꼼꼼히.",
                "바닥에 뭐가 남아 있던 것 같은데… 못 본 걸로 하자.",
            },
            [EveningMood.WorkLost] = new[]
            {
                "기다리다 그냥 간 손님이 자꾸 생각난다.",
                "줄이 길어서 손님을 놓쳤다. 계산을 더 빨리 해야지.",
            },
            [EveningMood.PackNone] = new[]
            {
                "오늘은 팩을 하나도 못 뜯었네. 손이 근질근질하다.",
                "카드팩 냄새도 못 맡은 하루…",
            },
            [EveningMood.PackPlain] = new[]
            {
                "팩 {packs}개 뜯었는데 별거 없었다. 내일은 터지겠지.",
                "오늘 팩은 조용했다. 운은 아껴 두는 거야.",
            },
            [EveningMood.PackNew] = new[]
            {
                "새 카드가 {new}장이나 늘었다. 바인더 채우는 맛!",
                "도감이 {new}칸 채워졌다. 집에 가서 정리해야지.",
            },
            [EveningMood.PackSuperRare] = new[]
            {
                "수퍼레어가 반짝이던 순간, 아직 눈에 선하다.",
                "오늘 수퍼레어 하나 건졌다. 나쁘지 않아!",
            },
            [EveningMood.PackUnique] = new[]
            {
                "유니크라니… 손이 아직도 떨린다.",
                "오늘 유니크 뽑은 거, 커뮤니티에 자랑해도 되겠지?",
            },
            [EveningMood.PackLegendary] = new[]
            {
                "레전더리를 뽑은 날이다. 오늘은 잠이 안 오겠다.",
                "꿈 아니지? 오늘 진짜 대박 났다!",
            },
        };

        /// <summary>오늘 일한 모습. 아쉬운 일(떠난 손님 · 못 한 청소)이 먼저, 그다음 바쁨 · 깨끗함.</summary>
        public static EveningMood WorkMoodFor(DayLedger today)
        {
            if (today == null) return EveningMood.WorkPlain;
            if (today.customersLost > 0) return EveningMood.WorkLost;
            if (today.cleanupsMissed > 0) return EveningMood.WorkMessy;
            if (today.customersServed >= BusyCustomers) return EveningMood.WorkBusy;
            if (today.cleanupsDone > 0) return EveningMood.WorkClean;
            return EveningMood.WorkPlain;
        }

        /// <summary>오늘 카드팩 결과. 높은 등급이 먼저.</summary>
        public static EveningMood PackMoodFor(DayLedger today)
        {
            if (today == null || today.packsOpened == 0) return EveningMood.PackNone;
            if (today.godPack || today.legendaryPack || today.bestRarity >= (int)CardRarity.Legendary) return EveningMood.PackLegendary;
            if (today.bestRarity >= (int)CardRarity.Unique) return EveningMood.PackUnique;
            if (today.bestRarity >= (int)CardRarity.SuperRare) return EveningMood.PackSuperRare;
            if (today.newCards >= ManyNewCards) return EveningMood.PackNew;
            return EveningMood.PackPlain;
        }

        /// <summary>두 줄 혼잣말 (일 한 줄 + 카드 한 줄).</summary>
        /// <param name="custom">EveningLineSet에서 파싱한 문장들. 없거나 해당 상황이 비어 있으면 기본 문장.</param>
        public static string For(int day, DayLedger today, IReadOnlyDictionary<EveningMood, List<string>> custom = null)
        {
            string work = Line(WorkMoodFor(today), day, today, custom);
            string pack = Line(PackMoodFor(today), day, today, custom);
            return work + "\n" + pack;
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
        public static Dictionary<EveningMood, List<string>> Parse(string raw)
        {
            var result = new Dictionary<EveningMood, List<string>>();
            if (string.IsNullOrEmpty(raw)) return result;
            foreach (string rawLine in raw.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int bar = line.IndexOf('|');
                if (bar <= 0) continue;
                string moodName = line.Substring(0, bar).Trim();
                string text = line.Substring(bar + 1).Trim();
                if (text.Length == 0 || !System.Enum.TryParse(moodName, true, out EveningMood mood)) continue;
                if (!System.Enum.IsDefined(typeof(EveningMood), mood)) continue;
                if (!result.TryGetValue(mood, out List<string> list)) result[mood] = list = new List<string>();
                list.Add(text);
            }
            return result;
        }
    }
}
