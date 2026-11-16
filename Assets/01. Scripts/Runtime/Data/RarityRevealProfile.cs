using System;
using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>
    /// 등급별 공개 연출 예산 (개봉연출 설계 3장). 좋은 등급일수록 멈춤이 길고 진동이 셉니다.
    /// 소리는 클립을 넣은 등급만 재생합니다(비워 두면 무음).
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Rarity Reveal Profile", fileName = "RarityRevealProfile")]
    public class RarityRevealProfile : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("이 카드가 맨 앞에 보인 뒤 다음 카드로 넘길 수 있기까지 (초)")]
            [Min(0)] public float pause;
            [Range(0, 1)] public float hapticAmplitude;
            [Min(0)] public float hapticSeconds;
            [Tooltip("진동 횟수 (유니크는 2번)")]
            [Min(1)] public int hapticCount = 1;
            public AudioClip sound;
            [Range(0, 1)] public float volume = 0.8f;

            public Entry(float pause, float amplitude, float seconds, int count = 1)
            {
                this.pause = pause;
                hapticAmplitude = amplitude;
                hapticSeconds = seconds;
                hapticCount = count;
            }
        }

        [Tooltip("CardRarity 순서: 노말, 레어, 수퍼레어, 유니크, 레전더리")]
        public Entry[] entries =
        {
            // 멈춤은 실수로 두 장을 한 번에 넘기지 않도록 노말·레어도 조금 둡니다 (설계 표보다 약간 김).
            new Entry(0.15f, 0.1f, 0.03f),
            new Entry(0.25f, 0.2f, 0.05f),
            new Entry(0.4f, 0.4f, 0.1f),
            new Entry(0.6f, 0.6f, 0.15f, 2),
            new Entry(1.0f, 1.0f, 0.3f),
        };

        public Entry Get(CardRarity rarity)
        {
            if (entries == null || entries.Length == 0) return new Entry(0.15f, 0.1f, 0.03f);
            return entries[Mathf.Clamp((int)rarity, 0, entries.Length - 1)];
        }
    }
}
