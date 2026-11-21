using CardShop.Cards;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 등급별 공개 연출의 "크기" (폴리싱 설계 P2 계단식, 11-22). 한 등급 올라갈 때마다 요소를 하나씩 더합니다.
    /// 레어 = 옛 수퍼레어 연출(가볍게) · 수퍼레어 = 더 화려하게 · 유니크 = 레전더리보다 조금 덜 · 레전더리 = 가장 화려하게.
    /// 노말은 연출 없음. 값은 미리 보기(98. Doc/폴리싱_미리보기.html)와 같게 맞춘 것이고 Quest 확인 뒤 여기서 조정합니다.
    /// 멈춤 · 진동 · 소리는 그대로 <see cref="RarityRevealProfile"/>에 있습니다.
    /// </summary>
    public sealed class RevealTier
    {
        public float Seconds;       // 연출 전체 길이 (다음 카드로 넘기면 바로 꺼짐)
        public float Halo;          // 뒤 후광 지름 (카드 높이의 배수)
        public bool HaloBreathe;    // 후광이 숨쉬듯 커졌다 작아짐
        public int[] Blobs;         // 슬라임 방울 폭죽 겹마다 개수
        public float Radius;        // 방울이 퍼지는 거리 (m)
        public int Stars;           // 테 둘레 별 반짝임
        public int Rays;            // 회전 빛줄기
        public int CounterRays;     // 반대로 도는 빛줄기 (레전더리)
        public float RayAlpha;
        public int EdgePulses;      // 카드 테 빛 맥박 횟수
        public int Rings;           // 앞으로 퍼지는 충격파 링
        public float Dim;           // 카드 둘레 어둡게 (알파)
        public int Confetti;        // 위에서 떨어지는 색종이
        public int Punches;         // 카드 말랑 펀치 횟수
        public float PunchScale;
        public bool Lift;           // 둥실 떠올랐다 내려옴
        public float ShakeSeconds;  // 카드 부르르 (0 = 없음)
        public Color[] Colors;      // 등급 색 (레전더리는 무지개 6색)
        public bool Rainbow => Colors.Length > 1;

        static readonly Color[] RainbowColors =
        {
            new Color(1f, 0.56f, 0.64f), new Color(1f, 0.76f, 0.42f), new Color(1f, 0.94f, 0.54f),
            new Color(0.56f, 0.94f, 0.64f), new Color(0.56f, 0.83f, 1f), new Color(0.77f, 0.61f, 1f),
        };

        static readonly RevealTier Rare = new RevealTier
        {
            Seconds = 0.6f, Halo = 1.4f, Blobs = new[] { 8 }, Radius = 0.07f, Punches = 1, PunchScale = 1.08f,
            Colors = new[] { new Color(0.75f, 0.89f, 1f) },
        };
        static readonly RevealTier SuperRare = new RevealTier
        {
            Seconds = 1.1f, Halo = 1.9f, Blobs = new[] { 12, 8 }, Radius = 0.10f, Stars = 8, Rays = 10, RayAlpha = 0.22f,
            EdgePulses = 1, Rings = 1, Punches = 1, PunchScale = 1.11f,
            Colors = new[] { new Color(0.9f, 0.74f, 1f) },
        };
        static readonly RevealTier Unique = new RevealTier
        {
            Seconds = 1.5f, Halo = 2.3f, HaloBreathe = true, Blobs = new[] { 16, 10 }, Radius = 0.13f, Stars = 10, Rays = 12, RayAlpha = 0.26f,
            EdgePulses = 2, Rings = 2, Dim = 0.3f, Confetti = 14, Punches = 2, PunchScale = 1.11f, ShakeSeconds = 0.45f,
            Colors = new[] { new Color(1f, 0.86f, 0.45f) },
        };
        static readonly RevealTier Legendary = new RevealTier
        {
            Seconds = 2.2f, Halo = 2.9f, HaloBreathe = true, Blobs = new[] { 20, 14, 10 }, Radius = 0.17f, Stars = 14, Rays = 16, CounterRays = 10,
            RayAlpha = 0.3f, EdgePulses = 3, Rings = 3, Dim = 0.45f, Confetti = 24, Punches = 2, PunchScale = 1.11f, Lift = true,
            ShakeSeconds = 0.9f, Colors = RainbowColors,
        };

        /// <summary>노말이면 null (연출 없음).</summary>
        public static RevealTier For(CardRarity rarity) => rarity switch
        {
            CardRarity.Rare => Rare,
            CardRarity.SuperRare => SuperRare,
            CardRarity.Unique => Unique,
            CardRarity.Legendary => Legendary,
            _ => null,
        };

        /// <summary>i번째 요소의 색 (무지개면 돌려 가며)</summary>
        public Color ColorAt(int i) => Colors[((i % Colors.Length) + Colors.Length) % Colors.Length];
    }
}
