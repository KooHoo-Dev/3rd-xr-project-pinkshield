using System;

namespace CardShop.Logic
{
    /// <summary>
    /// 등장 시계 (순수 C#, SpawnClockTests): 하루 일정표(영업 시작부터 몇 초에 나올지)를 따라 "지금 한 명 나올 때"를 알려 줍니다.
    /// 시간은 부르는 쪽이 흘려 줍니다(일시정지 · 이미 나와 있는 동안은 Tick을 부르지 않으면 멈춤).
    /// 카드 도둑이 쓰고, 손님 등장도 같은 방식이라 나중에 함께 씁니다 (큰_클래스_나누기_제안 2장).
    /// </summary>
    public sealed class SpawnClock
    {
        float[] schedule = Array.Empty<float>();
        int next;
        float elapsed;

        /// <summary>오늘 일정이 남아 있고 멈추지 않았음.</summary>
        public bool Running { get; private set; }
        /// <summary>아직 나오지 않은 차례가 있음.</summary>
        public bool HasPending => Running && next < schedule.Length;

        /// <summary>하루 시작: 일정표를 새로 받음 (오름차순 초).</summary>
        public void Start(float[] times)
        {
            schedule = times ?? Array.Empty<float>();
            next = 0;
            elapsed = 0f;
            Running = schedule.Length > 0;
        }

        /// <summary>더 내보내지 않음 (마감 · 강제 종료).</summary>
        public void Stop() => Running = false;

        /// <summary>시간을 dt만큼 흘리고, 이번에 차례가 됐으면 true (한 번에 한 차례씩).</summary>
        public bool Tick(float dt)
        {
            if (!Running) return false;
            elapsed += dt;
            if (next >= schedule.Length || schedule[next] > elapsed) return false;
            next++;
            return true;
        }
    }
}
