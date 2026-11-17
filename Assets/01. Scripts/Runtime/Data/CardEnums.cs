using System;

namespace CardShop.Cards
{
    // 값은 직렬화(에셋 저장)에 쓰이므로 명시합니다. 중간에 끼워 넣지 말고 뒤에 추가하세요.

    /// <summary>카드 종류 (기획서 5-3)</summary>
    public enum CardKind
    {
        Slime = 0,  // 슬라임
        Field = 1,  // 환경 필드
        Snack = 2,  // 슬라임 알 (예전 간식. 이름·ID 접두 SN은 직렬화·테스트 호환을 위해 유지)
    }

    /// <summary>슬라임 속성. 필드·간식 카드는 None. 무속성 슬라임은 Neutral.</summary>
    public enum CardElement
    {
        None = 0,
        Fire = 1,    // 불
        Water = 2,   // 물
        Earth = 3,   // 땅
        Air = 4,     // 공기
        Neutral = 5, // 무속성
    }

    /// <summary>희귀도. 값 순서가 곧 등급 순서입니다.</summary>
    public enum CardRarity
    {
        Normal = 0,     // 노말
        Rare = 1,       // 레어
        SuperRare = 2,  // 수퍼레어
        Unique = 3,     // 유니크
        Legendary = 4,  // 레전더리
    }

    /// <summary>선반 팩의 단서 (기획서 5-2). 팩 생성 시 고정되며, 플레이어가 뒤집어도 바뀌지 않습니다.</summary>
    [Flags]
    public enum PackClue
    {
        None = 0,
        FoldedTop = 1,   // 윗면 접힘
        FlippedBack = 2, // 뒷면이 위로 놓임
    }

    public static class CardEnumUtil
    {
        public const int RarityCount = 5;

        public static int ClueCount(PackClue clue)
        {
            int n = 0;
            if ((clue & PackClue.FoldedTop) != 0) n++;
            if ((clue & PackClue.FlippedBack) != 0) n++;
            return n;
        }
    }
}
