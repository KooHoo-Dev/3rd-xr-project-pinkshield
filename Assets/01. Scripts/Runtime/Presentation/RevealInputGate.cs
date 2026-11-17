namespace CardShop.Presentation
{
    /// <summary>
    /// 짧은 입력 잠금 타이머 (시연 피드백 B1·B2 보완 3).
    /// 마지막 장을 넘긴 오른손 트리거 눌림이 곧바로 "보관함에 넣기"가 되지 않도록, 잠근 뒤 정한 시간 동안 입력을 무시합니다.
    /// 시간은 밖에서 넘겨받는 순수 계산이라 EditMode 테스트로 고정합니다.
    /// </summary>
    public sealed class RevealInputGate
    {
        float openAt = float.NegativeInfinity;

        /// <summary>now부터 seconds 동안 잠급니다. 이미 더 길게 잠겨 있으면 그대로 둡니다.</summary>
        public void Lock(float now, float seconds)
        {
            float until = now + (seconds > 0f ? seconds : 0f);
            if (until > openAt) openAt = until;
        }

        public bool IsOpen(float now) => now >= openAt;

        public void Reset() => openAt = float.NegativeInfinity;
    }
}
