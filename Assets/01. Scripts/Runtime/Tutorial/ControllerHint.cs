namespace CardShop.Tutorial
{
    /// <summary>
    /// 안내 글에서 "어느 손의 어느 버튼"인지 읽음 (FIX C3, ControllerHintTests). 손이 없으면 양손.
    /// 예) "오른손 검지 트리거를 누른 채" → 오른손 트리거, "스캐너를 그립으로 꾹" → 양손 그립.
    /// </summary>
    public readonly struct ControllerHint
    {
        public readonly bool Left, Right, Trigger, Grip;

        ControllerHint(bool left, bool right, bool trigger, bool grip)
        {
            Left = left; Right = right; Trigger = trigger; Grip = grip;
        }

        public bool Any => (Left || Right) && (Trigger || Grip);

        public static ControllerHint Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return default;
            bool trigger = text.Contains("트리거"), grip = text.Contains("그립");
            bool left = text.Contains("왼손"), right = text.Contains("오른손");
            if (!left && !right) left = right = true;
            return new ControllerHint(left, right, trigger, grip);
        }
    }
}
