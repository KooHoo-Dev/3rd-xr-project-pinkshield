using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Logic;
using NUnit.Framework;
using UnityEngine;

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
        public void Waves_InsideWindow_Ascending_WithLullsBetweenWaves()
        {
            // 물결 2명씩 고정: 0~1번 / 2~3번 / 4~5번이 한 물결
            var w = new CampaignConfig.WaveSettings
            {
                waveSize = new Vector2Int(2, 2), inWaveGapSeconds = new Vector2(3f, 6f), secondsPerCustomer = 15f, lullSeconds = new Vector2(12f, 20f),
            };
            var rng = new SystemRandomSource(11);
            for (int k = 0; k < 100; k++)
            {
                float[] t = CustomerSchedule.Waves(6, 200f, w, rng);
                Assert.AreEqual(0f, t[0], "첫 손님은 영업 시작과 함께");
                for (int i = 1; i < t.Length; i++) Assert.That(t[i], Is.GreaterThanOrEqualTo(t[i - 1]));
                for (int i = 1; i < t.Length; i += 2) Assert.That(t[i] - t[i - 1], Is.InRange(3f, 6f), "같은 물결 안 간격");
                for (int i = 2; i < t.Length; i += 2)
                    Assert.That(t[i] - t[i - 2], Is.GreaterThanOrEqualTo(2 * 15f + 12f), "물결 사이에 계산 시간 + 한산한 시간이 있어야 함");
            }
        }

        [Test]
        public void Waves_CompressedIntoWindow_WhenTooLong()
        {
            var w = new CampaignConfig.WaveSettings { waveSize = new Vector2Int(1, 1), secondsPerCustomer = 30f, lullSeconds = new Vector2(30f, 30f) };
            float[] t = CustomerSchedule.Waves(5, 63f, w, new SystemRandomSource(2));
            Assert.AreEqual(63f, t[4], 1e-3f, "마지막 손님이 등장 구간 끝에 맞춰짐");
            for (int i = 1; i < t.Length; i++) Assert.That(t[i], Is.GreaterThan(t[i - 1]));
        }

        [Test]
        public void DefaultCampaign_WavesFitBusinessDays()
        {
            // 기본 설정에서 물결이 줄어들지 않고(= 한산한 시간이 남고) 등장 구간 안에 들어가는지: 최악(최대 인원, 가장 긴 간격)으로 확인
            var cfg = ScriptableObject.CreateInstance<CampaignConfig>();
            var w = new CampaignConfig.WaveSettings
            {
                waveSize = new Vector2Int(cfg.waves.waveSize.x, cfg.waves.waveSize.x), // 작은 물결 = 물결 수 최대
                inWaveGapSeconds = new Vector2(cfg.waves.inWaveGapSeconds.y, cfg.waves.inWaveGapSeconds.y),
                secondsPerCustomer = cfg.waves.secondsPerCustomer,
                lullSeconds = new Vector2(cfg.waves.lullSeconds.y, cfg.waves.lullSeconds.y),
            };
            for (int d = 1; d <= cfg.DayCount; d++)
            {
                CampaignConfig.DayEntry day = cfg.GetDay(d);
                float window = day.businessSeconds * cfg.spawnWindowRatio;
                float[] t = CustomerSchedule.Waves(day.customerMax, float.MaxValue, w, new SystemRandomSource(d));
                Assert.LessOrEqual(t[t.Length - 1], window * 1.1f, $"{d}일차: 손님 물결이 영업 시간에 비해 김 (10% 넘게 압축됨)");
            }
            Object.DestroyImmediate(cfg);
        }

        [Test]
        public void Schedule_ZeroCustomers_ReturnsEmpty()
        {
            var day = new CampaignConfig.DayEntry { businessSeconds = 90f, customerMin = 0, customerMax = 0 };
            Assert.IsEmpty(CustomerSchedule.Build(day, 0.7f, new SystemRandomSource(1)));
        }
    }
}
