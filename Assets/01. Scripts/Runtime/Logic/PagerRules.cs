namespace CardShop.Logic
{
    /// <summary>
    /// 모니터 쪽 넘기기 판정 (집_컴퓨터_화면_설계 J1). 순수 C#, PagerRulesTests.
    /// 화면을 왼쪽으로 끌면(dx &lt; 0) 다음 쪽, 오른쪽으로 끌면 이전 쪽. 화면 폭의 threshold 비율보다 짧게 끌면 제자리.
    /// 끝 쪽에서는 더 넘어가지 않음(돌지 않음).
    /// </summary>
    public static class PagerRules
    {
        public const float DragThreshold = 0.15f;

        /// <summary>끌기가 끝났을 때 갈 쪽 번호</summary>
        /// <param name="dx">끈 거리 (화면 로컬, 오른쪽이 +)</param>
        public static int AfterDrag(int page, int pageCount, float dx, float width, float threshold = DragThreshold)
        {
            if (width <= 0f || (dx < 0f ? -dx : dx) < width * threshold) return Clamp(page, pageCount);
            return Clamp(dx < 0f ? page + 1 : page - 1, pageCount);
        }

        public static int Clamp(int page, int pageCount) => page < 0 ? 0 : page >= pageCount ? (pageCount > 0 ? pageCount - 1 : 0) : page;
    }
}
