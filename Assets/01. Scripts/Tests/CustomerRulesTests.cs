using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;

namespace CardShop.Tests
{
    public class CustomerRulesTests
    {
        static List<CustomerLook> Looks(int local, int foreign)
        {
            var list = new List<CustomerLook>();
            for (int i = 0; i < local; i++) list.Add(CustomerLook.Local);
            for (int i = 0; i < foreign; i++) list.Add(CustomerLook.Foreign);
            return list;
        }

        [Test]
        public void Picker_RespectsGroupWeights_Roughly()
        {
            var looks = Looks(20, 20);
            var picker = new CustomerPicker(looks, 85, 15, 0, new SystemRandomSource(42));
            int foreign = 0;
            const int n = 4000;
            for (int i = 0; i < n; i++)
                if (looks[picker.Next()] == CustomerLook.Foreign) foreign++;
            Assert.That(foreign / (float)n, Is.EqualTo(0.15f).Within(0.03f));
        }

        [Test]
        public void Picker_FallsBackToOtherGroup_WhenGroupEmpty()
        {
            // 아직 아무도 Foreign으로 분류하지 않았어도 손님은 나와야 함
            var looks = Looks(5, 0);
            var picker = new CustomerPicker(looks, 0, 100, 0, new SystemRandomSource(1));
            for (int i = 0; i < 20; i++) Assert.That(picker.Next(), Is.InRange(0, 4));
        }

        [Test]
        public void Picker_AvoidsRecentLooks()
        {
            var looks = Looks(4, 0);
            var picker = new CustomerPicker(looks, 100, 0, 3, new SystemRandomSource(7));
            var last = new Queue<int>();
            for (int i = 0; i < 200; i++)
            {
                int pick = picker.Next();
                CollectionAssert.DoesNotContain(last, pick);
                last.Enqueue(pick);
                if (last.Count > 3) last.Dequeue();
            }
        }

        [Test]
        public void Picker_StillPicks_WhenFewerLooksThanNoRepeat()
        {
            var picker = new CustomerPicker(Looks(1, 0), 85, 15, 3, new SystemRandomSource(3));
            Assert.AreEqual(0, picker.Next());
            Assert.AreEqual(0, picker.Next());
        }

        [Test]
        public void Picker_EmptyRoster_ReturnsMinusOne()
        {
            var picker = new CustomerPicker(Looks(0, 0), 85, 15, 3, new SystemRandomSource(3));
            Assert.AreEqual(-1, picker.Next());
        }

        [Test]
        public void Schedule_CountWithinRange_AndInsideSpawnWindow()
        {
            var day = new CampaignConfig.DayEntry { businessSeconds = 90f, customerMin = 3, customerMax = 4 };
            var rng = new SystemRandomSource(5);
            for (int k = 0; k < 100; k++)
            {
                float[] times = CustomerSchedule.Build(day, 0.7f, rng);
                Assert.That(times.Length, Is.InRange(3, 4));
                for (int i = 0; i < times.Length; i++)
                {
                    Assert.That(times[i], Is.InRange(0f, 90f * 0.7f));
                    if (i > 0) Assert.That(times[i], Is.GreaterThanOrEqualTo(times[i - 1]));
                }
            }
        }

        [Test]
        public void Schedule_ZeroCustomers_ReturnsEmpty()
        {
            var day = new CampaignConfig.DayEntry { businessSeconds = 90f, customerMin = 0, customerMax = 0 };
            Assert.IsEmpty(CustomerSchedule.Build(day, 0.7f, new SystemRandomSource(1)));
        }
    }
}
