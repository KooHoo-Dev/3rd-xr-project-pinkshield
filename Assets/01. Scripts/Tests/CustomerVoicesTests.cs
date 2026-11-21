using System;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>손님 목소리 칸 고르기 (11-21 상황별 칸 · 성별, 나이 구분 없음).</summary>
    public class CustomerVoicesTests
    {
        [Test]
        public void EachMoment_UsesItsOwnSlot()
        {
            Assert.AreEqual(new[] { SoundId.CustomerComeMale }, CustomerVoices.Candidates(CustomerMoment.Enter, false));
            Assert.AreEqual(new[] { SoundId.CustomerComeFemale }, CustomerVoices.Candidates(CustomerMoment.Enter, true));
            Assert.AreEqual(new[] { SoundId.CustomerCheckoutMale }, CustomerVoices.Candidates(CustomerMoment.Checkout, false));
            Assert.AreEqual(new[] { SoundId.CustomerCheckoutFemale }, CustomerVoices.Candidates(CustomerMoment.Checkout, true));
            Assert.AreEqual(new[] { SoundId.CustomerBuyItmMale }, CustomerVoices.Candidates(CustomerMoment.Paid, false));
            Assert.AreEqual(new[] { SoundId.CustomerBuyItmFemale }, CustomerVoices.Candidates(CustomerMoment.Paid, true));
            Assert.AreEqual(new[] { SoundId.CustomerImpatientMale }, CustomerVoices.Candidates(CustomerMoment.Impatient, false));
            Assert.AreEqual(new[] { SoundId.CustomerImpatientFemale }, CustomerVoices.Candidates(CustomerMoment.Impatient, true));
            Assert.AreEqual(new[] { SoundId.CustomerAngry }, CustomerVoices.Candidates(CustomerMoment.Angry, false));
            Assert.AreEqual(new[] { SoundId.CustomerAngryFemale }, CustomerVoices.Candidates(CustomerMoment.Angry, true));
        }

        [Test]
        public void BoughtCard_FallsBackToBoughtItem()
        {
            Assert.AreEqual(new[] { SoundId.CustomerBuyCardMale, SoundId.CustomerBuyItmMale },
                CustomerVoices.Candidates(CustomerMoment.PaidCard, false));
            Assert.AreEqual(new[] { SoundId.CustomerBuyCardFemale, SoundId.CustomerBuyItmFemale },
                CustomerVoices.Candidates(CustomerMoment.PaidCard, true));
        }

        [Test]
        public void BrowseAndWaiting_AreBubbleOnly()
        {
            foreach (bool female in new[] { false, true })
            {
                Assert.IsEmpty(CustomerVoices.Candidates(CustomerMoment.Browse, female));
                Assert.IsEmpty(CustomerVoices.Candidates(CustomerMoment.Waiting, female));
            }
        }

        [Test]
        public void Women_NeverGetMaleSlots_AndMenNeverGetFemaleSlots()
        {
            foreach (CustomerMoment moment in Enum.GetValues(typeof(CustomerMoment)))
            {
                foreach (SoundId id in CustomerVoices.Candidates(moment, true))
                    StringAssert.EndsWith("Female", id.ToString(), $"{moment} 여자");
                foreach (SoundId id in CustomerVoices.Candidates(moment, false))
                    StringAssert.DoesNotEndWith("Female", id.ToString(), $"{moment} 남자");
            }
        }
    }
}
