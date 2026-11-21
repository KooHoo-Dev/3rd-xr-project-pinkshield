using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>잡기 · 떨어짐 소리 고르기 (11-21 피드백 S3 · S5).</summary>
    public class GrabSoundRulesTests
    {
        [Test]
        public void Components_WinOverName()
        {
            Assert.AreEqual(GrabKind.Trash, GrabSoundRules.Classify(true, false, false, "CustomerItem_Box"));
            Assert.AreEqual(GrabKind.Pack, GrabSoundRules.Classify(false, true, false, "Scanner"));
            Assert.AreEqual(GrabKind.Box, GrabSoundRules.Classify(false, false, true, "Pack"));
        }

        [Test]
        public void Name_GuessesCounterItems()
        {
            Assert.AreEqual(GrabKind.Box, GrabSoundRules.Classify(false, false, false, "CustomerItem_Box(Clone)"));
            Assert.AreEqual(GrabKind.Pack, GrabSoundRules.Classify(false, false, false, "CustomerItem_Pack(Clone)"));
            Assert.AreEqual(GrabKind.Card, GrabSoundRules.Classify(false, false, false, "CustomerItem_Card(Clone)"));
            Assert.AreEqual(GrabKind.Item, GrabSoundRules.Classify(false, false, false, "BarcodeScanner")); // "can"이 들어 있어도 쓰레기 아님
            Assert.AreEqual(GrabKind.Item, GrabSoundRules.Classify(false, false, false, "PaperBag"));
            Assert.AreEqual(GrabKind.Item, GrabSoundRules.Classify(false, false, false, null));
        }

        [Test]
        public void EveryKind_HasPickAndFallSound()
        {
            Assert.AreEqual(SoundId.PackPick, GrabSoundRules.PickSound(GrabKind.Pack));
            Assert.AreEqual(SoundId.BoxPick, GrabSoundRules.PickSound(GrabKind.Box));
            Assert.AreEqual(SoundId.TrashPick, GrabSoundRules.PickSound(GrabKind.Trash));
            Assert.AreEqual(SoundId.CardPick, GrabSoundRules.PickSound(GrabKind.Card));
            Assert.AreEqual(SoundId.ItemPick, GrabSoundRules.PickSound(GrabKind.Item));
            Assert.AreEqual(SoundId.TrashFall, GrabSoundRules.FallSound(GrabKind.Trash));
            Assert.AreEqual(SoundId.PackFall, GrabSoundRules.FallSound(GrabKind.Pack));
            Assert.AreEqual(SoundId.PackFall, GrabSoundRules.FallSound(GrabKind.Card));
            Assert.AreEqual(SoundId.BoxPick, GrabSoundRules.FallSound(GrabKind.Box));
        }

        [Test]
        public void SoftPlacement_IsSilent_HardDropIsLouder()
        {
            Assert.AreEqual(0f, GrabSoundRules.ImpactVolume(0.3f));
            float soft = GrabSoundRules.ImpactVolume(GrabSoundRules.MinImpactSpeed);
            float hard = GrabSoundRules.ImpactVolume(5f);
            Assert.That(soft, Is.GreaterThan(0f));
            Assert.That(hard, Is.GreaterThan(soft));
            Assert.That(hard, Is.LessThanOrEqualTo(1f));
        }
    }
}
