using System;
using CardShop.Core;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>단계가 플레이어를 얼마나 붙잡는가 (튜토리얼_설계 0장).</summary>
    public enum TutorialStepKind
    {
        Say = 0,   // 말만. 진행을 막지 않음
        Wait = 1,  // 그 행동을 할 때까지 기다림 (시간이 흐르지 않는 페이즈)
        Hold = 2,  // 게임 시간(영업 타이머 · 손님 · 도둑)을 멈추고 기다림
    }

    /// <summary>누가 말하는가.</summary>
    public enum TutorialSpeaker
    {
        Boss = 0,  // 사장님 말풍선 (가게)
        Self = 1,  // 알바생 혼잣말 (집)
    }

    /// <summary>
    /// 튜토리얼 한 단계. 순서는 <see cref="TutorialSequence"/>의 목록 순서입니다.
    /// 시작: <see cref="phase"/> · 일차 범위 안에서, <see cref="after"/> 단계가 끝났고(비어 있으면 상관없음),
    /// <see cref="startOn"/> 신호가 오면 시작합니다. startOn이 None이면 after 단계가 끝나는 즉시 시작합니다.
    /// 끝: <see cref="doneOn"/> 신호가 오면 끝납니다. None이면 <see cref="seconds"/>초 뒤 끝납니다.
    /// 시작하려는 순간 doneOn 신호를 이미 받은 적이 있으면(플레이어가 먼저 해 버림) 보여 주지 않고 끝난 것으로 칩니다.
    /// </summary>
    [Serializable]
    public sealed class TutorialStep
    {
        [Tooltip("단계 이름 (after에서 가리킴, 같은 시퀀스 안에서 겹치지 않게)")]
        public string key;
        [Tooltip("이 페이즈에서만 시작합니다. 페이즈가 바뀌면 진행 중이던 단계는 취소되어 다음 기회에 다시 나옵니다.")]
        public GamePhaseId phase = GamePhaseId.Business;
        [Min(1)] public int fromDay = 1;
        [Tooltip("0이면 끝 없음")]
        [Min(0)] public int toDay;
        [Tooltip("먼저 끝나야 하는 단계 key (비우면 없음)")]
        public string after;
        public TutorialSignal startOn = TutorialSignal.PhaseEntered;
        public TutorialSignal doneOn = TutorialSignal.None;
        [Tooltip("doneOn이 None이면 이 시간 뒤 끝남. doneOn이 있으면 말풍선을 최소 이만큼은 보여 줌 (0 = 최소 시간 없음)")]
        [Min(0f)] public float seconds = 4f;
        [Tooltip("doneOn이 있어도 이 시간이 지나면 끝냄 (0 = 끝까지 기다림)")]
        [Min(0f)] public float timeout;
        public TutorialStepKind kind = TutorialStepKind.Say;
        public TutorialSpeaker speaker = TutorialSpeaker.Boss;
        [Tooltip("하이라이트할 HighlightTarget.key 목록. 비우면 말풍선을 눈앞에 띄움")]
        public string[] targets = Array.Empty<string>();
        [Tooltip("{packPrice} 같은 자리표시는 TutorialLineFormatter가 채움. 줄바꿈은 \\n")]
        [TextArea(2, 3)] public string line;
        [Tooltip("가만히 있으면(IdleHint) 띄울 손 힌트. 비우면 없음")]
        public string idleHint;

        [Tooltip("켜면: doneOn 신호를 이 단계가 시작되기 전에(같은 날) 이미 받았어도 건너뛰지 않음. " +
                 "도둑 안내처럼 끝 신호(대걸레 잡기)가 다른 일(얼룩 닦기)로도 먼저 생기는 단계용 (11-21 2차 B3)")]
        public bool doneOnlyAfterStart;

        public bool InDay(int day) => day >= fromDay && (toDay <= 0 || day <= toDay);
    }
}
