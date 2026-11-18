using System.Collections.Generic;
using CardShop.Cards;

namespace CardShop.Logic
{
    /// <summary>
    /// 손님 외형 뽑기 (타이틀연출_손님_설계 3-1). 그룹(Local/Foreign)을 가중치로 고르고, 그룹 안에서 무작위로 고릅니다.
    /// 최근에 나온 외형은 피하고, 뽑은 그룹에 후보가 없으면 다른 그룹에서 고릅니다.
    /// </summary>
    public sealed class CustomerPicker
    {
        readonly IReadOnlyList<CustomerLook> looks;
        readonly int localWeight;
        readonly int foreignWeight;
        readonly int noRepeatCount;
        readonly IRandomSource random;
        readonly List<int> recent = new List<int>();

        public CustomerPicker(IReadOnlyList<CustomerLook> looks, int localWeight, int foreignWeight, int noRepeatCount, IRandomSource random)
        {
            this.looks = looks;
            this.localWeight = localWeight < 0 ? 0 : localWeight;
            this.foreignWeight = foreignWeight < 0 ? 0 : foreignWeight;
            this.noRepeatCount = noRepeatCount < 0 ? 0 : noRepeatCount;
            this.random = random;
        }

        /// <summary>목록의 인덱스. 목록이 비었으면 -1.</summary>
        public int Next()
        {
            if (looks.Count == 0) return -1;

            CustomerLook group = RollGroup();
            int pick = PickIn(group, avoidRecent: true);
            if (pick < 0) pick = PickIn(Other(group), avoidRecent: true);
            if (pick < 0) pick = PickIn(group, avoidRecent: false);
            if (pick < 0) pick = PickIn(Other(group), avoidRecent: false);

            recent.Add(pick);
            while (recent.Count > noRepeatCount) recent.RemoveAt(0);
            return pick;
        }

        CustomerLook RollGroup()
        {
            int total = localWeight + foreignWeight;
            if (total <= 0) return CustomerLook.Local;
            return random.Range(0, total) < localWeight ? CustomerLook.Local : CustomerLook.Foreign;
        }

        static CustomerLook Other(CustomerLook g) => g == CustomerLook.Local ? CustomerLook.Foreign : CustomerLook.Local;

        int PickIn(CustomerLook group, bool avoidRecent)
        {
            var candidates = new List<int>();
            for (int i = 0; i < looks.Count; i++)
                if (looks[i] == group && !(avoidRecent && recent.Contains(i)))
                    candidates.Add(i);
            return candidates.Count == 0 ? -1 : candidates[random.Range(0, candidates.Count)];
        }
    }

    /// <summary>
    /// 하루 손님 등장 시각 (설계서 Q1). 손님 수는 그날 범위 안에서 뽑고,
    /// 등장은 영업 시간의 앞쪽 spawnWindowRatio 안에 고르게 나눈 뒤 칸마다 조금씩 흔듭니다.
    /// </summary>
    public static class CustomerSchedule
    {
        /// <param name="waves">손님 물결 설정 (기기테스트_피드백_설계 P1). null이면 예전처럼 고르게 분산</param>
        /// <returns>영업 시작부터 잰 등장 시각(초), 오름차순</returns>
        public static float[] Build(CampaignConfig.DayEntry day, float spawnWindowRatio, IRandomSource random, CampaignConfig.WaveSettings waves = null)
        {
            int min = day.customerMin, max = day.customerMax < day.customerMin ? day.customerMin : day.customerMax;
            int count = random.Range(min, max + 1);
            if (count <= 0) return new float[0];

            float window = day.businessSeconds * spawnWindowRatio;
            if (waves != null) return Waves(count, window, waves, random);
            float slot = window / count;
            var times = new float[count];
            for (int i = 0; i < count; i++)
            {
                // 첫 손님은 바로 오도록 칸의 앞쪽 절반 안에서만 흔듦
                float jitter = (float)random.NextDouble() * slot * 0.5f;
                times[i] = i * slot + jitter;
            }
            return times;
        }

        /// <summary>
        /// 손님 물결 (P1): 2~3명이 몇 초 간격으로 몰려오고, 그 손님들을 계산할 만큼의 시간 + 한산한 시간이 지난 뒤 다음 물결.
        /// 한산한 시간에 청소 · 도둑 대처를 할 수 있게 하되, 계산이 느리면 다음 물결과 겹쳐 긴장이 생깁니다(사건 조율은 하지 않음).
        /// 전체가 등장 구간(window)을 넘으면 비율대로 줄여 구간 안에 넣습니다.
        /// </summary>
        public static float[] Waves(int count, float window, CampaignConfig.WaveSettings w, IRandomSource random)
        {
            var times = new float[count];
            int minSize = w.waveSize.x < 1 ? 1 : w.waveSize.x;
            int maxSize = w.waveSize.y < minSize ? minSize : w.waveSize.y;

            float waveStart = 0f;
            int i = 0;
            while (i < count)
            {
                int size = random.Range(minSize, maxSize + 1);
                if (size > count - i) size = count - i;
                float t = waveStart;
                for (int k = 0; k < size; k++, i++)
                {
                    if (k > 0) t += Between(w.inWaveGapSeconds, random);
                    times[i] = t;
                }
                // 다음 물결: 이번 물결 첫 손님부터 (계산 예상 시간 × 인원) + 한산한 시간
                waveStart += size * w.secondsPerCustomer + Between(w.lullSeconds, random);
                if (waveStart < t) waveStart = t;
            }

            float last = times[count - 1];
            if (last > window && last > 0f)
                for (int k = 0; k < count; k++) times[k] *= window / last;
            return times;
        }

        static float Between(UnityEngine.Vector2 range, IRandomSource random) =>
            range.x + (float)random.NextDouble() * (range.y > range.x ? range.y - range.x : 0f);
    }
}
