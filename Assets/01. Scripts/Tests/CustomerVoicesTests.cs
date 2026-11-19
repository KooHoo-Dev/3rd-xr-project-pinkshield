using System;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    /// <summary>손님 목소리 칸 고르기 (11-19 사운드 연결: 성별 · 나이 · 상황별).</summary>
    public class CustomerVoicesTests
    {
        [Test]
        public void Moments_MapToKinds()
        {
            Assert.AreEqual(VoiceKind.Wow, CustomerVoices.KindFor(CustomerMoment.PaidCard));
            Assert.AreEqual(VoiceKind.Impatient, CustomerVoices.KindFor(CustomerMoment.Impatient));
            Assert.AreEqual(VoiceKind.Angry, CustomerVoices.KindFor(CustomerMoment.Angry));
            Assert.AreEqual(VoiceKind.Light, CustomerVoices.KindFor(CustomerMoment.Enter));
            Assert.AreEqual(VoiceKind.Light, CustomerVoices.KindFor(CustomerMoment.Paid));
        }

        [Test]
        public void EveryCombination_HasAtLeastOneSlot_AndNoMaleSlotForWomen()
        {
            foreach (VoiceKind kind in Enum.GetValues(typeof(VoiceKind)))
                foreach (bool female in new[] { false, true })
                    foreach (bool young in new[] { false, true })
                    {
                        SoundId[] ids = CustomerVoices.Candidates(kind, female, young);
                        Assert.IsNotEmpty(ids, $"{kind} female={female} young={young}");
                        if (female)
                            CollectionAssert.DoesNotContain(ids, SoundId.CustomerAngry, "여자 손님이 남자 화남 소리를 내면 안 됨");
                    }
        }

        [Test]
        public void YoungWoman_PrefersGirlSlot_ThenFallsBackToAdult()
        {
            Assert.AreEqual(new[] { SoundId.CustomerWowGirl, SoundId.CustomerWowFemale },
                CustomerVoices.Candidates(VoiceKind.Wow, true, true));
            Assert.AreEqual(new[] { SoundId.CustomerVoiceGirl, SoundId.CustomerVoiceFemale },
                CustomerVoices.Candidates(VoiceKind.Light, true, true));
        }

        [Test]
        public void Man_UsesAdultSlots_RegardlessOfAge()
        {
            foreach (VoiceKind kind in Enum.GetValues(typeof(VoiceKind)))
                Assert.AreEqual(CustomerVoices.Candidates(kind, false, false), CustomerVoices.Candidates(kind, false, true));
            Assert.AreEqual(new[] { SoundId.CustomerAngry }, CustomerVoices.Candidates(VoiceKind.Angry, false, false));
        }
    }
}
