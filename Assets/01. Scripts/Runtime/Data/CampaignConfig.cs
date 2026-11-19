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
        // 2 = 창문 얼룩: 업무가 많아져 제외 (DEC-014). 저장된 값이 꼬이지 않게 번호는 비워 둠
        MessyPack = 4, // 흐트러진 팩
        FloorStain = 8, // 바닥 얼룩 · 대걸레 (DEC-030, F5에서 만들 때까지는 CleanupDirector가 '만들 수 없음'으로 빼 둠)
        All = Trash | MessyPack | FloorStain,
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
            [Tooltip("손님이 다 나오고 다 나간 뒤 남은 청소가 없으면 남은 영업 시간을 빨리 감음 (FIX D6, 1일차)")]
            public bool fastForwardWhenIdle;
        }

        public List<DayEntry> days = new List<DayEntry>
        {
            // 11-19 A9: 손님이 적게 느껴짐 → 영업 시간 · 손님 수 상향 (물결 크기 2~3은 그대로, 물결 수가 늘어남)
            new DayEntry { businessSeconds = 120f, customerMin = 4, customerMax = 4, fastForwardWhenIdle = true },
            new DayEntry { businessSeconds = 135f, customerMin = 5, customerMax = 6 },
            new DayEntry { businessSeconds = 135f, customerMin = 5, customerMax = 6 },
            new DayEntry { businessSeconds = 150f, customerMin = 6, customerMax = 7 },
            new DayEntry { businessSeconds = 150f, customerMin = 6, customerMax = 7 },
            new DayEntry { businessSeconds = 165f, customerMin = 7, customerMax = 8 },
            new DayEntry { businessSeconds = 165f, customerMin = 8, customerMax = 9 },
        };

        [Header("영업 규칙 (설계서 Q1, Q2)")]
        [Tooltip("손님 스폰을 영업 시간의 앞쪽 이 비율 안에 분산")]
        [Range(0.1f, 1f)] public float spawnWindowRatio = 0.7f;
        [Tooltip("마지막 손님 퇴장 후 마감 정리 시간(초)")]
        public float closingCleanupSeconds = 30f;
        [Tooltip("한가할 때 빨리 감는 배율 (DayEntry.fastForwardWhenIdle)")]
        [Min(1f)] public float idleFastForward = 6f;
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
            public float secondsPerCustomer = 10f; // 11-19 A9: 15 → 10 (손님이 늘어도 영업 시간 안에 물결이 다 나오게)
            [Tooltip("물결 사이 한산한 시간(초): 청소 · 도둑에 대처할 틈")]
            public Vector2 lullSeconds = new Vector2(7f, 11f); // 11-19 A9: 12~20 → 7~11
        }

        /// <summary>청소 거리 생성 (빌드피드백_11-19_FIX설계 F1 · F2, DEC-030): 손님이 나갈 때마다 무조건 → 확률로, 종류는 균등 → 가중치</summary>
        [Serializable]
        public class CleanupSpawnSettings
        {
            [Tooltip("손님이 나갈 때 청소 거리가 생길 확률 (2일차부터)")]
            [Range(0f, 1f)] public float chance = 0.35f;
            [Tooltip("1일차 확률. 1일차 첫 청소는 튜토리얼 때문에 반드시 생김")]
            [Range(0f, 1f)] public float firstDayChance = 0.25f;
            [Tooltip("동시에 남아 있을 수 있는 청소 거리 수 (이만큼 있으면 더 안 생김)")]
            [Min(1)] public int maxActive = 3;
            [Tooltip("이 수만큼 연속으로 안 생기면 다음 손님 때는 반드시 생김 (0 = 끔)")]
            [Min(0)] public int guaranteeAfter = 4;
            [Header("종류 가중치 (그날 켜진 종류끼리 비율)")]
            [Min(0)] public int trashWeight = 35;
            [Min(0)] public int messyPackWeight = 20;
            [Min(0)] public int floorStainWeight = 45;

            public int WeightOf(CleanupKinds kind) => kind switch
            {
                CleanupKinds.Trash => trashWeight,
                CleanupKinds.MessyPack => messyPackWeight,
                CleanupKinds.FloorStain => floorStainWeight,
                _ => 0,
            };
        }

        [Header("청소 거리 생성 (F1 · F2)")]
        public CleanupSpawnSettings cleanup = new CleanupSpawnSettings();

        [Header("손님 물결 (P1)")]
        [Tooltip("끄면 예전처럼 등장 구간에 고르게 분산")]
        public bool useWaves = true;
        public WaveSettings waves = new WaveSettings();

        /// <summary>CustomerSchedule.Build에 넘길 물결 설정 (끄면 null)</summary>
        public WaveSettings Waves => useWaves ? waves : null;

        /// <summary>카드 도둑 (카드도둑_설계 4장, 2026-11-18 확정: 3일차부터 하루 1번, 빗자루 한 대, 놓치면 그날 무료 팩 없음)</summary>
        [Serializable]
        public class ThiefSettings
        {
            [Tooltip("도둑이 처음 나오는 일차")]
            public int firstDay = 3;
            [Tooltip("하루에 나오는 도둑 수 (0이면 안 나옴)")]
            public int perDay = 1;
            [Tooltip("등장 시각 = 영업 시간 × 이 비율 사이 무작위 (최소, 최대)")]
            public Vector2 spawnWindow = new Vector2(0.25f, 0.6f);
            [Tooltip("팩을 들고 달아나는 속도 (m/s). 플레이어 이동 2.5 m/s보다 느리게")]
            public float runSpeed = 1.8f;
            [Tooltip("훔칠 자리에 와서 팩을 집기 전 머뭇거리는 시간 (초). 이때 쳐도 잡힘")]
            public float lingerSeconds = 1.5f;
            [Tooltip("팩을 집은 뒤 달아나기 전 약 올리는 시간 (초)")]
            public float tauntSeconds = 1f;
            [Tooltip("문 바깥에서 인도를 따라 이만큼 가면 놓침 (m)")]
            public float escapeDistance = 10f;
            [Tooltip("빗자루 머리가 이 속도(m/s) 이상이어야 맞은 것으로 침")]
            public float hitSpeed = 1.0f;
            [Tooltip("도둑 몸 반지름 (m). 빗자루 머리가 이 안에 들어오면 맞음")]
            public float hitRadius = 0.35f;
            [Tooltip("도둑을 잡으면 줄 선 손님의 인내 시간을 이만큼 되돌려 줌 (초, 영웅 보너스)")]
            public float heroPatienceBonus = 10f;
        }

        [Header("카드 도둑")]
        public ThiefSettings thief = new ThiefSettings();

        [Header("선반 (설계서 Q5)")]
        public int shelfSlotCount = 12;

        public int DayCount => days.Count;
        /// <summary>마지막 날인가 (집을 건너뛰고 최종 결과로 감)</summary>
        public bool IsLastDay(int day) => day >= DayCount;

        /// <summary>1부터 시작하는 일차.</summary>
        public DayEntry GetDay(int day) => days[Mathf.Clamp(day - 1, 0, days.Count - 1)];
    }
}
