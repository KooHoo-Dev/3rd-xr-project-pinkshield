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
        /// <returns>영업 시작부터 잰 등장 시각(초), 오름차순</returns>
        public static float[] Build(CampaignConfig.DayEntry day, float spawnWindowRatio, IRandomSource random)
        {
            int min = day.customerMin, max = day.customerMax < day.customerMin ? day.customerMin : day.customerMax;
            int count = random.Range(min, max + 1);
            if (count <= 0) return new float[0];

            float window = day.businessSeconds * spawnWindowRatio;
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
    }
}
