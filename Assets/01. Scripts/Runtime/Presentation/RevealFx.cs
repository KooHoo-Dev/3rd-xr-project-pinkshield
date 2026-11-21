using System;
using System.Collections;
using CardShop.Cards;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Presentation
{
    /// <summary>
    /// 카드 공개 순간의 등급 연출 (개봉연출 설계 5장, DEC-004 → 폴리싱 설계 P2 계단식, 11-22): 소리 · 진동 · 슬라임 방울 폭죽 · 빛(<see cref="RevealAura"/>) · 카드 펀치 · 흔들림.
    /// 레어부터 한 등급마다 요소가 하나씩 늘어납니다(<see cref="RevealTier"/>). 카메라 셰이크는 VR 멀미 우려로 쓰지 않습니다.
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
            [Tooltip("갓팩 예고: 짧은 진동 3번 뒤 이어지는 길고 센 진동 (초). 11-19 피드백: 더 극적으로")]
            public float GodRumbleSeconds = 1f;
            [Tooltip("갓팩 예고 효과음 (비워 두면 없음)")]
            public AudioClip GodPackSound;
        }

        readonly MonoBehaviour host; // 진동 여러 번(코루틴) · 폭죽 부모
        readonly Func<CardRarityStyles> styles;
        readonly Settings settings;
        readonly RevealBurst[] bursts = new RevealBurst[3]; // 겹 폭죽 (레전더리 3겹)
        RevealAura aura;
        RarityRevealProfile defaultProfile;

        /// <param name="styles">등급 색 (카드 프리팹의 것을 빌려 씀, 없으면 흰색)</param>
        public RevealFx(MonoBehaviour host, Func<CardRarityStyles> styles, Settings settings)
        {
            this.host = host;
            this.styles = styles;
            this.settings = settings;
        }

        /// <summary>폭죽 오브젝트를 미리 만들어 둠 (퇴근 후 데우기).</summary>
        public void Prewarm()
        {
            for (int i = 0; i < bursts.Length; i++) Burst(i);
            Aura();
        }

        /// <summary>진행 중인 폭죽을 멈춤 (전체 정리).</summary>
        public void Stop()
        {
            foreach (RevealBurst b in bursts) if (b != null) b.Stop();
            if (aura != null) aura.Stop();
        }

        /// <summary>
        /// 맨 앞 카드 공개 연출. 다음으로 넘길 수 있기까지의 멈춤(초)을 돌려줍니다.
        /// </summary>
        /// <param name="legendaryPack">레전더리 확정 팩의 레전더리 카드: 금빛 폭죽 + 더 멈춤 + 진동 1번 더</param>
        public float Announce(CardRevealView view, CardData card, bool legendaryPack, XRBaseInputInteractor hand)
        {
            Settings s = settings;
            // 11-22: 유니크도 자기 칸 (수퍼레어와 통일하던 것을 계단식으로 나눔)
            RarityRevealProfile.Entry fx = Profile(s).Get(card.rarity);
            if (fx.sound != null) AudioSource.PlayClipAtPoint(fx.sound, view.transform.position, fx.volume);
            int count = fx.hapticCount + (legendaryPack ? 1 : 0);
            if (hand != null) host.StartCoroutine(Haptic(hand, fx.hapticAmplitude, fx.hapticSeconds, count));

            Stop(); // 앞 카드 연출이 남아 있으면 바로 끔 (넘기면 즉시 꺼짐)
            RevealTier tier = RevealTier.For(card.rarity);
            if (tier != null) Celebrate(view, tier, legendaryPack);
            return fx.pause + (legendaryPack ? s.LegendaryPackExtraPause : 0f);
        }

        void Celebrate(CardRevealView view, RevealTier tier, bool legendaryPack)
        {
            Transform t = view.transform;
            float scale = t.lossyScale.x;
            Aura().Play(tier, new Pose(t.position, t.rotation), scale);

            // 슬라임 방울: 카드 앞면을 가리지 않게 카드 1cm 뒤에서 퍼짐 (일부는 앞으로 튀어나옴). 레전더리 확정 팩은 금빛을 섞고 조금 크게
            Vector3 behind = t.position + t.forward * 0.01f;
            Color[] palette = legendaryPack ? new[] { SpecialGold, tier.ColorAt(0), SpecialGold, tier.ColorAt(3) } : tier.Colors;
            float radius = tier.Radius * scale * (legendaryPack ? 1.15f : 1f);
            for (int i = 0; i < tier.Blobs.Length && i < bursts.Length; i++)
                Burst(i).Play(behind, t.rotation, palette, radius * (1f - 0.25f * i), tier.Seconds * 0.85f, tier.Blobs[i], i * 0.17f);

            view.Punch(tier.PunchScale, tier.Punches);
            if (tier.Lift) view.Lift(0.012f, tier.Seconds);
            if (tier.ShakeSeconds > 0f)
            {
                bool legendary = view.Card != null && view.Card.rarity == CardRarity.Legendary;
                view.Shake(settings.LegendaryShakeAmount * (legendary ? 1f : 0.5f), legendary ? settings.LegendaryShakeSeconds : tier.ShakeSeconds);
            }
        }

        /// <summary>갓팩 예고: 카드 묶음 앞에서 큰 금빛 폭죽 + 진동 3번 + (있으면) 효과음. 첫 카드까지 기다릴 시간(초)을 돌려줍니다.</summary>
        public float GodIntro(Pose stack, XRBaseInputInteractor hand)
        {
            Vector3 center = stack.position + stack.rotation * Vector3.forward * 0.01f;
            Burst(0).Play(center, stack.rotation, SpecialGold, 0.3f, 1.4f);
            if (settings.GodPackSound != null) AudioSource.PlayClipAtPoint(settings.GodPackSound, center, 1f);
            const float pulse = 0.12f, gap = 0.06f;
            if (hand != null) host.StartCoroutine(GodHaptic(hand, pulse, gap, settings.GodRumbleSeconds));
            // 긴 진동이 끝나기 전에 첫 카드가 나오지 않게
            float hapticTotal = 3f * pulse + 3f * gap + settings.GodRumbleSeconds;
            return Mathf.Max(settings.GodIntroSeconds, hapticTotal);
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

        RevealBurst Burst(int i) => bursts[i] != null ? bursts[i] : bursts[i] = RevealBurst.Create(host.transform);
        RevealAura Aura() => aura != null ? aura : aura = RevealAura.Create(host.transform);

        // 두근두근(짧게 3번) → 부르르(길고 세게)
        static IEnumerator GodHaptic(XRBaseInputInteractor hand, float pulse, float gap, float rumble)
        {
            yield return Haptic(hand, 0.8f, pulse, 3);
            yield return new WaitForSeconds(pulse + gap);
            if (hand != null && rumble > 0f) hand.SendHapticImpulse(1f, rumble);
        }

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
