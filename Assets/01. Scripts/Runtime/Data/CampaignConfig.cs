using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Cards
{
    [Flags]
    public enum CleanupKinds
    {
        None = 0,
        Trash = 1,     // 바닥 쓰레기
        Smudge = 2,    // 창문 얼룩
        MessyPack = 4, // 흐트러진 팩
        All = Trash | Smudge | MessyPack,
    }

    /// <summary>
    /// 일차 진행 설정 (기획서 5-1, 8장 축소안). 일수를 줄이려면 days 목록만 줄이면 됩니다.
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Config/Campaign", fileName = "CampaignConfig")]
    public class CampaignConfig : ScriptableObject
    {
        [Serializable]
        public class DayEntry
        {
            public float businessSeconds = 90f;
            public int customerMin = 3;
            public int customerMax = 4;
            public CleanupKinds cleanupKinds = CleanupKinds.All;
        }

        public List<DayEntry> days = new List<DayEntry>
        {
            new DayEntry { customerMin = 3, customerMax = 4 },
            new DayEntry { customerMin = 3, customerMax = 4 },
            new DayEntry { customerMin = 4, customerMax = 5 },
            new DayEntry { customerMin = 4, customerMax = 5 },
            new DayEntry { customerMin = 5, customerMax = 6 },
            new DayEntry { customerMin = 5, customerMax = 6 },
            new DayEntry { customerMin = 5, customerMax = 6 },
        };

        [Header("영업 규칙 (설계서 Q1, Q2)")]
        [Tooltip("손님 스폰을 영업 시간의 앞쪽 이 비율 안에 분산")]
        [Range(0.1f, 1f)] public float spawnWindowRatio = 0.7f;
        [Tooltip("마지막 손님 퇴장 후 마감 정리 시간(초)")]
        public float closingCleanupSeconds = 30f;
        [Tooltip("결제를 기다리는 손님의 인내 시간(초)")]
        public float customerPatienceSeconds = 30f;

        [Header("선반 (설계서 Q5)")]
        public int shelfSlotCount = 12;

        public int DayCount => days.Count;

        /// <summary>1부터 시작하는 일차.</summary>
        public DayEntry GetDay(int day) => days[Mathf.Clamp(day - 1, 0, days.Count - 1)];
    }
}
