using CardShop.Cards;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    public class RevealProfileTests
    {
        // 개봉연출 설계 3장: 좋은 등급일수록 멈춤이 길고 진동이 세야 "연출 길이 = 등급 신호"가 됩니다.
        [Test]
        public void DefaultProfile_GrowsWithRarity()
        {
            var p = ScriptableObject.CreateInstance<RarityRevealProfile>();
            try
            {
                Assert.AreEqual(5, p.entries.Length, "노말~레전더리 5개");
                for (int i = 1; i < p.entries.Length; i++)
                {
                    Assert.GreaterOrEqual(p.entries[i].pause, p.entries[i - 1].pause, $"{(CardRarity)i} 멈춤");
                    Assert.GreaterOrEqual(p.entries[i].hapticAmplitude, p.entries[i - 1].hapticAmplitude, $"{(CardRarity)i} 진동");
                }
                Assert.AreEqual(2, p.Get(CardRarity.Unique).hapticCount, "유니크는 진동 2번");
                Assert.AreEqual(1f, p.Get(CardRarity.Legendary).hapticAmplitude);
            }
            finally { Object.DestroyImmediate(p); }
        }
    }
}
