namespace CardShop.Logic
{
    /// <summary>진열장에 세우기 결과 (거부 이유를 화면에 알리기 위함).</summary>
    public enum ShowcaseResult
    {
        Ok,
        NotOwned,
        Full,
    }

    /// <summary>
    /// 방 진열장 규칙 (집_업그레이드_설계 2장 E1, 순수 C# · ShowcaseRulesTests).
    /// - 판매 등록과 같은 방식: 진열하면 카드가 보유 목록(Collection)에서 빠져 GameSession.Showcase로 갑니다.
    ///   그래서 즉시 판매 · 모두 팔기 · 판매 등록에 걸리지 않습니다. 도감은 그대로입니다.
    /// - 진열 카드에 따로 점수는 없습니다 (DEC-031 Q4). 최종 점수의 카드 가치에는 그대로 들어갑니다(ScoreRules).
    /// - 등급 제한 없음. 칸 수는 진열장 단계에 따라 (UpgradeRules.ShowcaseSlots).
    /// </summary>
    public static class ShowcaseRules
    {
        /// <summary>진열장 업그레이드 전 칸 수 (설계서 3장 표: Lv1 3칸)</summary>
        public const int BaseSlots = 3;

        public static bool HasRoom(GameSession s, int capacity) => s.Showcase.Count < capacity;

        /// <summary>그 카드를 몇 장 진열했나 (바인더 칸의 "진열 n" 표시).</summary>
        public static int CountOf(GameSession s, string cardId)
        {
            int n = 0;
            foreach (string id in s.Showcase) if (id == cardId) n++;
            return n;
        }

        /// <summary>보유한 카드 1장을 진열합니다.</summary>
        public static ShowcaseResult TryAdd(GameSession s, string cardId, int capacity)
        {
            if (!HasRoom(s, capacity)) return ShowcaseResult.Full;
            if (!s.Collection.Remove(cardId)) return ShowcaseResult.NotOwned;
            s.Showcase.Add(cardId);
            return ShowcaseResult.Ok;
        }

        /// <summary>진열을 내리고 보유 목록으로 돌려놓습니다. 진열하지 않은 카드면 false.</summary>
        public static bool Remove(GameSession s, string cardId)
        {
            if (!s.Showcase.Remove(cardId)) return false;
            s.Collection.Add(cardId);
            return true;
        }
    }
}
