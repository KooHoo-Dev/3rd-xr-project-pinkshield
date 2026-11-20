namespace CardShop.Logic
{
    /// <summary>
    /// 정산 영수증 마지막에 찍는 도장 문구 (UI_개편_설계 4번 판). 그날 놓친 일(못 치운 곳 + 떠난 손님 + 도둑에게 뺏긴 팩)로 고릅니다.
    /// 순수 C#이라 테스트로 기준을 고정합니다 (SettlementStampTests).
    /// </summary>
    public static class SettlementStamp
    {
        public const string Great = "참 잘했어요";
        public const string Good = "수고했어요";
        public const string Weak = "분발!";

        /// <summary>놓친 일이 이 개수 이하면 "수고했어요"</summary>
        public const int GoodMaxMisses = 2;

        public static int Misses(DayLedger l) =>
            l == null ? 0 : l.cleanupsMissed + l.customersLost + (l.freePackStolen ? 1 : 0);

        public static string For(DayLedger l)
        {
            int m = Misses(l);
            if (m == 0) return Great;
            return m <= GoodMaxMisses ? Good : Weak;
        }
    }
}
