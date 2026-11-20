using System.Collections.Generic;

namespace CardShop.Logic
{
    /// <summary>
    /// 정산 판 "가구 덕분에" 줄 (경제시스템_설계 14-3, DEC-040): 오늘 가구가 해 준 일을 한 줄로 보여 줘서 사는 보람을 느끼게 합니다.
    /// 숫자는 DayLedger에 쌓인 것 (늘린 판매 칸 판매 · 침대 덕분에 기다려 준 손님 · 어젯밤 중고 장터 판매). 순수 C#.
    /// </summary>
    public static class UpgradeCredits
    {
        /// <summary>가구 덕분에 생긴 일이 없으면 null</summary>
        public static string Line(DayLedger l)
        {
            if (l == null) return null;
            var parts = new List<string>();
            if (l.extraSlotSales > 0) parts.Add($"늘린 판매 칸에서 {l.extraSlotSales}장 {MoneyText.Signed(l.extraSlotNet)}");
            if (l.patienceSaved > 0) parts.Add($"푹 자서 기다려 준 손님 {l.patienceSaved}명");
            if (!string.IsNullOrEmpty(l.onlineSaleCard)) parts.Add($"어젯밤 중고 장터 「{l.onlineSaleCard}」 {MoneyText.Signed(l.onlineSaleNet)}");
            return parts.Count == 0 ? null : "가구 덕분에: " + string.Join(" · ", parts);
        }

        /// <summary>침대가 없었으면 떠났을 손님인가 (기본 인내 시간은 넘겼지만 늘어난 시간 안에 계산)</summary>
        public static bool SavedByBed(float waitedSeconds, float basePatienceSeconds, float patienceMultiplier) =>
            patienceMultiplier > 1f && waitedSeconds > basePatienceSeconds;
    }
}
