using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>가게 배경 음악 고르기 (11-19: 1일차 고정 · 이후 번갈아 · 전날 유니크 이상을 뽑은 다음 날).</summary>
    public class ShopBgmTests
    {
        [Test]
        public void FirstDay_IsAlwaysTheDefaultSong()
        {
            Assert.AreEqual(SoundId.BgmShop, ShopBgm.Pick(1, -1, true));
            Assert.AreEqual(SoundId.BgmShop, ShopBgm.Pick(1, (int)CardRarity.Legendary, true));
        }

        [Test]
        public void LaterDays_UseTheSongChosenForTheDay()
        {
            Assert.AreEqual(SoundId.BgmShopChill, ShopBgm.Pick(2, -1, true));
            Assert.AreEqual(SoundId.BgmShop, ShopBgm.Pick(3, (int)CardRarity.SuperRare, false));
        }

        [Test]
        public void DayAfterUniqueOrBetter_PlaysTheLuckySong()
        {
            Assert.AreEqual(SoundId.BgmShopLucky, ShopBgm.Pick(2, (int)CardRarity.Unique, false));
            Assert.AreEqual(SoundId.BgmShopLucky, ShopBgm.Pick(5, (int)CardRarity.Legendary, true));
        }
    }
}
