using System;
using CardShop.Cards;
using CardShop.Core;

namespace CardShop.Logic
{
    /// <summary>
    /// 손님 말풍선과 함께 낼 목소리 칸 고르기 (11-21 상황별 칸, 나이 구분 없음).
    /// 상황 + 성별 → SoundBank 칸 후보를 앞에서부터. 앞 칸이 비어 있으면 다음 칸을 씁니다 (카드 산 칸이 비면 재고 산 칸).
    /// 구경 · 줄 기다림은 말풍선만 (같은 소리 반복 피로 줄이기). 순수 C#이라 CustomerVoicesTests로 고정합니다.
    /// </summary>
    public static class CustomerVoices
    {
        public static SoundId[] Candidates(CustomerMoment moment, bool female) => moment switch
        {
            CustomerMoment.Enter => new[] { female ? SoundId.CustomerComeFemale : SoundId.CustomerComeMale },
            CustomerMoment.Checkout => new[] { female ? SoundId.CustomerCheckoutFemale : SoundId.CustomerCheckoutMale },
            CustomerMoment.Paid => new[] { female ? SoundId.CustomerBuyItmFemale : SoundId.CustomerBuyItmMale },
            CustomerMoment.PaidCard => female
                ? new[] { SoundId.CustomerBuyCardFemale, SoundId.CustomerBuyItmFemale }
                : new[] { SoundId.CustomerBuyCardMale, SoundId.CustomerBuyItmMale },
            CustomerMoment.Impatient => new[] { female ? SoundId.CustomerImpatientFemale : SoundId.CustomerImpatientMale },
            CustomerMoment.Angry => new[] { female ? SoundId.CustomerAngryFemale : SoundId.CustomerAngry },
            _ => Array.Empty<SoundId>(), // 구경 · 줄 기다림
        };
    }
}
