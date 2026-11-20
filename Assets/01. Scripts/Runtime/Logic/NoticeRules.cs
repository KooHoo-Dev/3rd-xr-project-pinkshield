namespace CardShop.Logic
{
    /// <summary>알림 쪽지 종류: 띄우지 않음 · 경고(빨강 !) · 좋은 소식(초록 ★)</summary>
    public enum NoticeKind { Hidden, Warning, Good }

    /// <summary>
    /// RaiseNotice 문구 중 눈앞 쪽지로 띄울 것만 고름 (UI_개편_설계 알림: 빨강 ! / 초록). 순수 C#, NoticeRulesTests.
    /// 11-20 Play 피드백 "알림이 시도 때도 없이 와서 정신없음" → 빼는 목록 대신 **띄우는 목록**:
    /// 지금 손을 써야 하는 경고(도둑 · 바닥 청소 · 계산 실수 · 손님 이탈 · 안 되는 행동)와 놓치면 아쉬운 좋은 소식만.
    /// 팩 개봉 안내 · 손님 결제 · 마감 안내처럼 자막 · 결과판 · 벽시계가 이미 알려 주는 것은 띄우지 않음. 디버그 판은 모두 그대로 받음.
    /// </summary>
    public static class NoticeRules
    {
        static readonly string[] WarningPrefixes =
        {
            "도둑",                         // 도둑 등장 · 놓침 · 무료 팩 잃음
            "바닥에",                       // 쓰레기 · 얼룩
            "진열대 팩이 바닥에",           // 떨어진 팩
            "먼저 바코드", "먼저 스캐너",   // 계산 순서 실수
            "아직 봉투에",
            "손님이 기다리다 그냥 나갔어요",
            "퇴근은", "잠은",               // 지금 안 되는 행동
        };
        static readonly string[] GoodPrefixes = { "손님이 판매 등록한", "무료 팩을 되찾았어요", "[치트]" };

        public static NoticeKind Classify(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return NoticeKind.Hidden;
            foreach (string p in WarningPrefixes) if (message.StartsWith(p)) return NoticeKind.Warning;
            foreach (string p in GoodPrefixes) if (message.StartsWith(p)) return NoticeKind.Good;
            return NoticeKind.Hidden;
        }
    }
}
