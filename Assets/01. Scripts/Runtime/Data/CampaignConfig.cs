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
            // 손님 물결(P1)로 한산한 시간이 생기는 만큼 영업 시간을 늘림: 4~5명 135초, 5~6명 150초
            new DayEntry { businessSeconds = 135f, customerMin = 4, customerMax = 5 },
            new DayEntry { businessSeconds = 135f, customerMin = 4, customerMax = 5 },
            new DayEntry { businessSeconds = 150f, customerMin = 5, customerMax = 6 },
            new DayEntry { businessSeconds = 150f, customerMin = 5, customerMax = 6 },
            new DayEntry { businessSeconds = 150f, customerMin = 5, customerMax = 6 },
        };

        [Header("영업 규칙 (설계서 Q1, Q2)")]
        [Tooltip("손님 스폰을 영업 시간의 앞쪽 이 비율 안에 분산")]
        [Range(0.1f, 1f)] public float spawnWindowRatio = 0.7f;
        [Tooltip("마지막 손님 퇴장 후 마감 정리 시간(초)")]
        public float closingCleanupSeconds = 30f;
        [Tooltip("결제를 기다리는 손님의 인내 시간(초)")]
        public float customerPatienceSeconds = 30f;
        [Tooltip("계산대 물건이 2개째부터 한 개당 더 기다려 주는 시간(초) (계산시스템_확장_설계 K6)")]
        public float patiencePerExtraItemSeconds = 10f;

        /// <summary>손님 물결 (기기테스트_피드백_설계 P1, 2026-11-18 확정: 물결 A안, 사건 조율 B안은 쓰지 않음)</summary>
        [Serializable]
        public class WaveSettings
        {
            [Tooltip("한 물결의 손님 수 (최소, 최대)")]
            public Vector2Int waveSize = new Vector2Int(2, 3);
            [Tooltip("같은 물결 안 손님 사이 간격(초)")]
            public Vector2 inWaveGapSeconds = new Vector2(3f, 6f);
            [Tooltip("손님 한 명 계산에 걸린다고 보는 시간(초). 다음 물결은 (이 값 × 인원) + 한산한 시간 뒤에 옴")]
            public float secondsPerCustomer = 15f;
            [Tooltip("물결 사이 한산한 시간(초): 청소 · 도둑에 대처할 틈")]
            public Vector2 lullSeconds = new Vector2(12f, 20f);
        }

        [Header("손님 물결 (P1)")]
        [Tooltip("끄면 예전처럼 등장 구간에 고르게 분산")]
        public bool useWaves = true;
        public WaveSettings waves = new WaveSettings();

        /// <summary>CustomerSchedule.Build에 넘길 물결 설정 (끄면 null)</summary>
        public WaveSettings Waves => useWaves ? waves : null;

        [Header("선반 (설계서 Q5)")]
        public int shelfSlotCount = 12;

        public int DayCount => days.Count;

        /// <summary>1부터 시작하는 일차.</summary>
        public DayEntry GetDay(int day) => days[Mathf.Clamp(day - 1, 0, days.Count - 1)];
    }
}
