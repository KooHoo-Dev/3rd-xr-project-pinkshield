namespace CardShop.Logic
{
    /// <summary>
    /// 난수 추상화. 테스트는 고정 시드, 디버그 치트는 "다음 결과 강제" 구현으로 교체합니다.
    /// UnityEngine.Random을 직접 쓰지 마세요(재현과 테스트가 어려워짐).
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>[0, 1)</summary>
        double NextDouble();

        /// <summary>[minInclusive, maxExclusive)</summary>
        int Range(int minInclusive, int maxExclusive);
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        readonly System.Random random;

        public SystemRandomSource(int seed) => random = new System.Random(seed);
        public SystemRandomSource() => random = new System.Random();

        public double NextDouble() => random.NextDouble();
        public int Range(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);
    }
}
