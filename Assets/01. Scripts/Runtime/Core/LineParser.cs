using System;
using System.Collections.Generic;

namespace CardShop.Core
{
    /// <summary>
    /// 대사 묶음(아침 · 퇴근 · 손님) 공용 읽기: 한 줄에 "상황|문장". 빈 줄과 '#'로 시작하는 줄은 무시.
    /// 상황 이름은 enum 이름(대소문자 무시)이고, 알 수 없는 상황이나 문장이 빈 줄은 건너뜁니다.
    /// </summary>
    public static class LineParser
    {
        public static Dictionary<T, List<string>> Parse<T>(string raw) where T : struct, Enum
        {
            var result = new Dictionary<T, List<string>>();
            if (string.IsNullOrEmpty(raw)) return result;
            foreach (string rawLine in raw.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int bar = line.IndexOf('|');
                if (bar <= 0) continue;
                string name = line.Substring(0, bar).Trim();
                string text = line.Substring(bar + 1).Trim();
                // IsDefined: "9|문장"처럼 숫자로 쓴 없는 값은 TryParse가 통과시키므로 걸러 냄
                if (text.Length == 0 || !Enum.TryParse(name, true, out T key) || !Enum.IsDefined(typeof(T), key)) continue;
                if (!result.TryGetValue(key, out List<string> list)) result[key] = list = new List<string>();
                list.Add(text);
            }
            return result;
        }
    }
}
