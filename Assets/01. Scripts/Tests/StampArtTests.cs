using CardShop.Logic;
using CardShop.UI;
using NUnit.Framework;
using UnityEngine;

namespace CardShop.Tests
{
    /// <summary>정산 도장 그림 · 잉크 색 (StampArt, 11-20 플레이 F2).</summary>
    public class StampArtTests
    {
        [Test]
        public void EachWord_HasItsOwnFileAndInk()
        {
            Assert.AreEqual("Stamp_Great", StampArt.FileFor(SettlementStamp.Great));
            Assert.AreEqual("Stamp_Good", StampArt.FileFor(SettlementStamp.Good));
            Assert.AreEqual("Stamp_Weak", StampArt.FileFor(SettlementStamp.Weak));
            Assert.AreEqual(StampArt.GreatInk, StampArt.InkFor(SettlementStamp.Great));
            Assert.AreEqual(StampArt.GoodInk, StampArt.InkFor(SettlementStamp.Good));
            Assert.AreEqual(StampArt.WeakInk, StampArt.InkFor(SettlementStamp.Weak));
        }

        [Test]
        public void StampPictures_AreImportedAsSprites()
        {
            // 그림이 Sprite로 들어오지 않으면(StampTextureImporter) 게임에서 조용히 예전 도장으로 돌아가므로 여기서 잡음
            foreach (string word in new[] { SettlementStamp.Great, SettlementStamp.Good, SettlementStamp.Weak })
                Assert.IsNotNull(StampArt.SpriteFor(word), $"Resources/Stamps/{StampArt.FileFor(word)} 가 Sprite가 아님");
        }
    }
}
