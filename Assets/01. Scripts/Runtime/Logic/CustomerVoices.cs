using CardShop.Cards;
using CardShop.Core;

namespace CardShop.Logic
{
    /// <summary>손님 목소리의 종류 (말풍선 대사의 분위기)</summary>
    public enum VoiceKind { Light, Wow, Impatient, Angry }

    /// <summary>
    /// 손님 말풍선과 함께 낼 목소리 칸 고르기 (11-19 사운드 연결).
    /// 상황 → 종류(가벼운 반응 '오' · 와우 · 재촉 · 화남), 종류 + 성별 + 나이 → SoundBank 칸 후보를 앞에서부터.
    /// 앞 칸이 비어 있으면 다음 칸을 씁니다 (예: 젊은 여자 칸이 비면 어른 여자 칸). 순수 C#이라 CustomerVoicesTests로 고정합니다.
    /// </summary>
    public static class CustomerVoices
    {
        public static VoiceKind KindFor(CustomerMoment moment) => moment switch
        {
            CustomerMoment.PaidCard => VoiceKind.Wow,         // 찾던 카드를 삼
            CustomerMoment.Impatient => VoiceKind.Impatient,  // 줄 재촉
            CustomerMoment.Angry => VoiceKind.Angry,          // 기다리다 떠남
            _ => VoiceKind.Light,
        };

        /// <summary>칸 후보 (앞이 우선). 남자는 아직 나이 구분 칸이 없어 어른 칸을 씁니다.</summary>
        public static SoundId[] Candidates(VoiceKind kind, bool female, bool young)
        {
            if (!female)
            {
                return kind switch
                {
                    VoiceKind.Wow => new[] { SoundId.CustomerWowMale, SoundId.CustomerVoiceMale },
                    VoiceKind.Angry => new[] { SoundId.CustomerAngry },
                    _ => new[] { SoundId.CustomerVoiceMale }, // 남자 재촉 소리는 아직 없음
                };
            }
            return kind switch
            {
                VoiceKind.Wow => young
                    ? new[] { SoundId.CustomerWowGirl, SoundId.CustomerWowFemale }
                    : new[] { SoundId.CustomerWowFemale },
                VoiceKind.Impatient => new[] { SoundId.CustomerImpatientFemale, SoundId.CustomerVoiceFemale },
                VoiceKind.Angry => new[] { SoundId.CustomerAngryFemale },
                _ => young
                    ? new[] { SoundId.CustomerVoiceGirl, SoundId.CustomerVoiceFemale }
                    : new[] { SoundId.CustomerVoiceFemale },
            };
        }

        public static SoundId[] Candidates(CustomerMoment moment, bool female, bool young) =>
            Candidates(KindFor(moment), female, young);
    }
}
