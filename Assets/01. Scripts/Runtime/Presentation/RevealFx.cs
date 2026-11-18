using System;
using System.Collections;
using CardShop.Cards;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Presentation
{
    /// <summary>
    /// 카드 공개 순간의 등급 연출 (개봉연출 설계 5장, DEC-004): 소리 · 진동 · 폭죽 · 카드 흔들림.
    /// 수퍼레어~유니크는 같은 연출, 레전더리만 더 크게(큰 폭죽 + 카드 흔들림 + 진동). 카메라 셰이크는 VR 멀미 우려로 쓰지 않습니다.
    /// 등급별 멈춤 · 진동 · 소리 값은 <see cref="RarityRevealProfile"/>에 있습니다.
    /// CardRevealer(개봉 진행)가 부르고, 이 클래스는 CardRevealer를 모릅니다.
    /// </summary>
    public sealed class RevealFx
    {
        static readonly Color SpecialGold = new Color(1f, 0.82f, 0.3f);

        /// <summary>인스펙터에서 조정하는 값 (CardRevealer 인스펙터에 이 묶음으로 보임).</summary>
        [Serializable]
        public sealed class Settings
        {
            [Tooltip("등급별 멈춤 · 진동 · 소리. 비워 두면 설계 기본값")]
            public RarityRevealProfile Profile;
            [Tooltip("레전더리 카드가 부르르 떨리는 크기 (m)와 시간 (초)")]
            public float LegendaryShakeAmount = 0.004f;
            public float LegendaryShakeSeconds = 0.9f;
            [Tooltip("레전더리 확정 팩의 레전더리 카드는 평소보다 이만큼 더 멈춤 (초) + 진동 1번 더")]
            public float LegendaryPackExtraPause = 0.6f;
            [Tooltip("갓팩 예고 뒤 첫 카드까지 멈춤 (초)")]
            public float GodIntroSeconds = 1f;
            [Tooltip("갓팩 예고 효과음 (비워 두면 없음)")]
            public AudioClip GodPackSound;
        }

        readonly MonoBehaviour host; // 진동 여러 번(코루틴) · 폭죽 부모
        readonly Func<CardRarityStyles> styles;
        readonly Settings settings;
        RevealBurst burst;
        RarityRevealProfile defaultProfile;

        /// <param name="styles">등급 색 (카드 프리팹의 것을 빌려 씀, 없으면 흰색)</param>
        public RevealFx(MonoBehaviour host, Func<CardRarityStyles> styles, Settings settings)
        {
            this.host = host;
            this.styles = styles;
            this.settings = settings;
        }

        /// <summary>폭죽 오브젝트를 미리 만들어 둠 (퇴근 후 데우기).</summary>
        public void Prewarm() => Burst();

        /// <summary>진행 중인 폭죽을 멈춤 (전체 정리).</summary>
        public void Stop()
        {
            if (burst != null) burst.Stop();
        }

        /// <summary>
        /// 맨 앞 카드 공개 연출. 다음으로 넘길 수 있기까지의 멈춤(초)을 돌려줍니다.
        /// </summary>
        /// <param name="legendaryPack">레전더리 확정 팩의 레전더리 카드: 금빛 폭죽 + 더 멈춤 + 진동 1번 더</param>
        public float Announce(CardRevealView view, CardData card, bool legendaryPack, XRBaseInputInteractor hand)
        {
            Settings s = settings;
            CardRarity tier = card.rarity == CardRarity.Unique ? CardRarity.SuperRare : card.rarity; // 연출 등급 통일
            RarityRevealProfile.Entry fx = Profile(s).Get(tier);
            if (fx.sound != null) AudioSource.PlayClipAtPoint(fx.sound, view.transform.position, fx.volume);
            int count = fx.hapticCount + (legendaryPack ? 1 : 0);
            if (hand != null) host.StartCoroutine(Haptic(hand, fx.hapticAmplitude, fx.hapticSeconds, count));

            // 카드 앞면을 가리지 않게 카드 1cm 뒤에서 퍼짐
            Vector3 behind = view.transform.position + view.transform.forward * 0.01f;
            if (card.rarity == CardRarity.Legendary)
            {
                Burst().Play(behind, view.transform.rotation, legendaryPack ? SpecialGold : RarityColor(CardRarity.Legendary),
                    legendaryPack ? 0.34f : 0.3f, legendaryPack ? 1.5f : 1.3f);
                view.Shake(s.LegendaryShakeAmount, s.LegendaryShakeSeconds);
            }
            else if (card.rarity >= CardRarity.SuperRare)
            {
                Burst().Play(behind, view.transform.rotation, SpecialGold, 0.11f, 0.7f);
            }
            return fx.pause + (legendaryPack ? s.LegendaryPackExtraPause : 0f);
        }

        /// <summary>갓팩 예고: 카드 묶음 앞에서 큰 금빛 폭죽 + 진동 3번 + (있으면) 효과음. 첫 카드까지 기다릴 시간(초)을 돌려줍니다.</summary>
        public float GodIntro(Pose stack, XRBaseInputInteractor hand)
        {
            Vector3 center = stack.position + stack.rotation * Vector3.forward * 0.01f;
            Burst().Play(center, stack.rotation, SpecialGold, 0.3f, 1.4f);
            if (settings.GodPackSound != null) AudioSource.PlayClipAtPoint(settings.GodPackSound, center, 1f);
            if (hand != null) host.StartCoroutine(Haptic(hand, 0.8f, 0.12f, 3));
            return settings.GodIntroSeconds;
        }

        RarityRevealProfile Profile(Settings s)
        {
            if (s.Profile != null) return s.Profile;
            if (defaultProfile == null) defaultProfile = ScriptableObject.CreateInstance<RarityRevealProfile>();
            return defaultProfile;
        }

        Color RarityColor(CardRarity rarity)
        {
            CardRarityStyles st = styles();
            if (rarity == CardRarity.Normal || st == null) return Color.white;
            return st.Get(rarity).accentColor;
        }

        RevealBurst Burst() => burst != null ? burst : burst = RevealBurst.Create(host.transform);

        static IEnumerator Haptic(XRBaseInputInteractor hand, float amplitude, float seconds, int count)
        {
            for (int k = 0; k < count; k++)
            {
                if (k > 0) yield return new WaitForSeconds(seconds + 0.06f);
                if (hand == null) yield break;
                hand.SendHapticImpulse(amplitude, seconds);
            }
        }
    }
}
