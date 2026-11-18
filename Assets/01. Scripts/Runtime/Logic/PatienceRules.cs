using System;

namespace CardShop.Logic
{
    /// <summary>계산대 줄에서 기다린 정도. 대사 · 떠나기 판단에 씁니다.</summary>
    public enum PatienceStage
    {
        Calm = 0,       // 아직 괜찮음
        Waiting = 1,    // '기다리는 중' 대사를 할 때
        Impatient = 2,  // '재촉' 대사를 할 때
        Expired = 3,    // 인내 시간을 넘김 → 화내고 떠남 (감점 없음, 설계서 Q2)
    }

    /// <summary>
    /// 손님 인내 시간 규칙 (순수 C#, PatienceRulesTests). CustomerDirector에서 떼어 냄 (큰_클래스_나누기_제안 2장).
    /// </summary>
    public static class PatienceRules
    {
        /// <summary>
        /// 인내 시간(초) = 기본 + 계산 중이면 (물건 수 − 1) × 한 개당 추가 (계산시스템_확장_설계 K6: 물건이 많으면 더 기다려 줌).
        /// </summary>
        public static float Seconds(float baseSeconds, float perExtraItem, int itemCount, bool atCheckout) =>
            baseSeconds + (atCheckout ? perExtraItem * Math.Max(0, itemCount - 1) : 0f);

        /// <summary>기다린 시간이 인내 시간의 어디쯤인지. waitingAt · impatientAt은 0~1 비율.</summary>
        public static PatienceStage Stage(float waited, float patience, float waitingAt, float impatientAt)
        {
            if (patience <= 0f || waited >= patience) return PatienceStage.Expired;
            float ratio = waited / patience;
            if (ratio >= impatientAt) return PatienceStage.Impatient;
            if (ratio >= waitingAt) return PatienceStage.Waiting;
            return PatienceStage.Calm;
        }

        /// <summary>
        /// 이번에 할 대사 (없으면 Calm). 재촉은 한 번, '기다리는 중'은 재촉 전에 한 번만.
        /// 재촉까지 건너뛰어 왔으면 '기다리는 중'은 하지 않습니다.
        /// </summary>
        public static PatienceStage LineToSay(PatienceStage stage, bool saidWaiting, bool saidImpatient)
        {
            if (stage >= PatienceStage.Impatient) return saidImpatient ? PatienceStage.Calm : PatienceStage.Impatient;
            if (stage == PatienceStage.Waiting && !saidWaiting) return PatienceStage.Waiting;
            return PatienceStage.Calm;
        }
    }
}
