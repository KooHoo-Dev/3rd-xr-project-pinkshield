using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 대사 다듬기 (튜토리얼_설계 8-1). {packPrice} 같은 자리표시를 값으로 채우고,
    /// 인스펙터에 적은 두 글자 "\n"을 실제 줄바꿈으로 바꾸며, 말풍선에 들어가는 길이인지 검사합니다.
    /// </summary>
    public static class TutorialLineFormatter
    {
        static readonly Regex Unfilled = new Regex(@"\{\w+\}");

        /// <summary>values에 있는 {이름}만 채움. 없는 이름은 그대로 둡니다.</summary>
        public static string Format(string line, IReadOnlyDictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;
            var sb = new StringBuilder(line.Length + 16);
            int i = 0;
            while (i < line.Length)
            {
                char c = line[i];
                // 백슬래시 + n → 줄바꿈
                if (c == '\\' && i + 1 < line.Length && line[i + 1] == 'n')
                {
                    sb.Append('\n');
                    i += 2;
                    continue;
                }
                if (c == '{' && values != null)
                {
                    int close = line.IndexOf('}', i + 1);
                    if (close > i + 1)
                    {
                        string name = line.Substring(i + 1, close - i - 1);
                        if (values.TryGetValue(name, out string value))
                        {
                            sb.Append(value);
                            i = close + 1;
                            continue;
                        }
                    }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>채워지지 않은 {이름}이 남았는가.</summary>
        public static bool HasUnfilled(string formatted)
        {
            return !string.IsNullOrEmpty(formatted) && Unfilled.IsMatch(formatted);
        }

        /// <summary>줄 수와 줄당 글자 수가 한도 안인가.</summary>
        public static bool Fits(string formatted, int maxLines = 2, int maxCharsPerLine = 32)
        {
            if (string.IsNullOrEmpty(formatted)) return true;
            string[] lines = formatted.Split('\n');
            if (lines.Length > maxLines) return false;
            foreach (string l in lines)
                if (l.TrimEnd('\r').Length > maxCharsPerLine) return false;
            return true;
        }
    }
}
