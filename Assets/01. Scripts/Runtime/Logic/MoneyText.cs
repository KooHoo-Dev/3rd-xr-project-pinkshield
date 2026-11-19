using System.Globalization;

namespace CardShop.Logic
{
    /// <summary>
    /// 돈 글자는 모두 여기서 만듭니다 (빌드피드백_11-19_FIX설계 B2: 달러 표시, 내부 값 그대로).
    /// 표시 방식을 바꿀 때 이 파일만 고치면 됩니다.
    /// </summary>
    public static class MoneyText
    {
        /// <summary>$1,250</summary>
        public static string Format(int v) => v < 0 ? "−$" + (-v).ToString("N0", CultureInfo.InvariantCulture) : "$" + v.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>+$12 / −$3 / $0 (빼기는 ResultTexts.Signed와 같은 유니코드 마이너스)</summary>
        public static string Signed(int v) => v > 0 ? "+" + Format(v) : Format(v);
    }
}
