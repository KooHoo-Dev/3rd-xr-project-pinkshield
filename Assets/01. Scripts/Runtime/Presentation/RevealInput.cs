using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Presentation
{
    /// <summary>
    /// 카드 넘기기 입력 (시연 피드백 B3): 오른손 트리거 한 번 = 다음 카드, 꾹 누르기 = 건너뛰기.
    /// 꾹 누르는 동안 게이지(<see cref="SkipHoldGauge"/>)를 채우고 점점 세지는 진동으로 눌림이 전달되고 있음을 알립니다.
    /// 시간 판정은 <see cref="HoldToSkipTimer"/>(순수 계산, 테스트됨)가 하고, 이 클래스는 손 · 게이지 · 진동을 잇습니다.
    /// CardRevealer(개봉 진행)가 부르고, 이 클래스는 CardRevealer를 모릅니다.
    /// </summary>
    public sealed class RevealInput
    {
        public enum Result { Next, Skip }

        /// <summary>CardRevealer 인스펙터 값 (씬에 저장된 값을 그대로 쓰기 위해 CardRevealer에 둠).</summary>
        public struct Settings
        {
            public float HoldSeconds;      // 이 시간 이상 누르면 건너뛰기
            public float GaugeGrace;       // 이보다 짧은 눌림(탭)에는 게이지를 보이지 않음
            public float HapticMin;        // 꾹 누르는 동안 진동 세기: 처음
            public float HapticMax;        //                          끝
        }

        readonly Func<SkipHoldGauge> createGauge;
        readonly Func<Settings> settings;
        SkipHoldGauge gauge;
        HoldToSkipTimer timer;

        /// <param name="createGauge">게이지를 처음 쓸 때 만드는 방법 (글꼴 · 부모는 CardRevealer가 앎)</param>
        public RevealInput(Func<SkipHoldGauge> createGauge, Func<Settings> settings)
        {
            this.createGauge = createGauge;
            this.settings = settings;
        }

        /// <summary>마지막 <see cref="WaitForAdvance"/>의 결과.</summary>
        public Result Last { get; private set; }

        /// <summary>개봉을 시작할 때. 자르기 제스처로 트리거를 누른 채 열렸을 수 있어, 한 번 뗄 때까지 꾹 누르기를 세지 않음.</summary>
        public void Begin()
        {
            Settings s = settings();
            timer = new HoldToSkipTimer(s.HoldSeconds, s.GaugeGrace);
            timer.RequireRelease();
        }

        /// <summary>게이지를 미리 만들어 둠 (퇴근 후 데우기, 첫 꾹 누르기 때 끊김 방지).</summary>
        public void Prewarm() => Gauge();

        public void HideGauge()
        {
            if (gauge != null) gauge.Hide();
        }

        /// <summary>
        /// 트리거를 새로 누르면 다음(Next), 꾹 누르면 건너뛰기(Skip). 결과는 <see cref="Last"/>에.
        /// pause 동안은 넘기지 않음(실수로 두 장을 넘기지 않게). 손을 못 찾으면 pause + 1초 뒤 자동으로 다음.
        /// </summary>
        /// <param name="allowSkip">false면 꾹 눌러도 건너뛰지 않음 (갓팩, DEC-008)</param>
        public IEnumerator WaitForAdvance(XRBaseInputInteractor hand, float pause, bool allowSkip, Pose gaugePose)
        {
            if (timer == null) Begin();
            Settings s = settings();
            float readyAt = Time.time + pause;
            float autoAt = readyAt + 1f;
            float nextPulse = 0f;
            while (true)
            {
                float now = Time.time;
                bool held = hand != null && hand.activateInput.ReadIsPerformed();
                bool fired = timer.Update(held, now);
                if (allowSkip)
                {
                    if (fired)
                    {
                        HideGauge();
                        if (hand != null) hand.SendHapticImpulse(0.8f, 0.12f);
                        Last = Result.Skip;
                        yield break;
                    }
                    if (timer.IsCharging(now))
                    {
                        float progress = timer.Progress(now);
                        Gauge().Show(gaugePose, progress);
                        if (hand != null && now >= nextPulse)
                        {
                            // 누를수록 조금씩 세짐 (눌림이 전달되고 있다는 신호)
                            hand.SendHapticImpulse(Mathf.Lerp(s.HapticMin, s.HapticMax, progress), 0.1f);
                            nextPulse = now + 0.09f;
                        }
                    }
                    else HideGauge();
                }

                if (now >= readyAt && (hand != null ? hand.activateInput.ReadWasPerformedThisFrame() : now >= autoAt))
                {
                    Last = Result.Next;
                    yield break;
                }
                yield return null;
            }
        }

        SkipHoldGauge Gauge() => gauge != null ? gauge : gauge = createGauge();
    }
}
