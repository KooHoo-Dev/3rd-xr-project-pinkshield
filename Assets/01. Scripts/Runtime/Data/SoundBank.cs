using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>
    /// 게임에서 소리를 내는 순간 (11-19 필수 사운드 목록, DevLog/플레이피드백_11-19_작업계획 4장).
    /// 값을 바꾸면 에셋의 칸이 어긋나므로 끝에만 추가합니다.
    /// 등급 연출 소리(수퍼레어~유니크 · 레전더리)는 RarityRevealProfile, 갓팩은 CardRevealer fx 묶음의 GodPackSound가 원본이라 여기 없습니다.
    /// </summary>
    public enum SoundId
    {
        // 카드
        CardFlip = 0,        // 한 장 넘기기
        CardShuffle = 1,     // 상자 · 건너뛰기 (여러 장)
        PackTear = 2,        // 팩 자르기 (단일 팩 · 상자 팩마다)
        NewCard = 3,         // 처음 얻은 카드
        PileLand = 4,        // 카드가 더미에 내려앉음
        PileStash = 5,       // 더미가 보관함으로 날아감
        // 가게
        ScanBeep = 10,       // 바코드 (비우면 코드로 만든 삑)
        PosPay = 11,         // 결제 완료
        BagPut = 12,         // 봉투에 담기
        TrashDrop = 13,      // 쓰레기통에 쓰레기
        RackInsert = 14,     // 팩 꽂이에 꽂기
        BroomSwing = 15,
        BroomHit = 16,       // 도둑을 맞힘
        MopScrub = 17,       // 대걸레로 얼룩 문지르기 (FIX F5, 문지르는 동안 짧게 반복)
        StainClean = 18,     // 얼룩을 다 지움
        // 손님 · 도둑
        // 손님 목소리는 CustomerVoices가 상황(가벼운 반응 · 와우 · 재촉 · 화남) · 성별 · 나이로 칸을 고름
        CustomerVoiceMale = 20,   // 남자 가벼운 반응 (오)
        CustomerVoiceFemale = 21, // 여자(어른) 가벼운 반응
        CustomerAngry = 22,       // 남자 화남 (기다리다 떠남)
        ThiefLaughMale = 23,      // 도둑 등장 힌트
        ThiefLaughFemale = 24,
        ThiefCaught = 25,
        ThiefEscaped = 26,
        // 플레이어 · UI
        Footstep = 30,
        UiClick = 31,
        UiDeny = 32,         // "열 수 없어요" 같은 거절
        PauseOpen = 33,
        MoneyIn = 34,        // 정산 · 판매로 돈이 들어옴
        TutorialDing = 35,   // 튜토리얼 자막이 새로 뜸 (FIX B1)
        // 집 · 하루
        HomeDoor = 40,
        LightSwitch = 41,
        Sleep = 42,
        Rooster = 43,        // 아침
        ResignStamp = 44,    // 사직서 도장 (무한 모드 끝내기, 비면 BagPut)
        // 배경 (BgmPlayer가 반복 재생)
        BgmTitle = 50,
        BgmShop = 51,
        BgmHome = 52,
        AmbienceStreet = 53,
        // 손님 목소리 추가 (11-19, 성별 · 나이 · 상황별)
        CustomerWowMale = 60,
        CustomerWowFemale = 61,
        CustomerAngryFemale = 62,
        CustomerImpatientFemale = 63, // 한숨 · 헛기침 (줄 재촉)
        CustomerVoiceGirl = 64,       // 젊은 여자 가벼운 반응 (비면 어른 칸)
        CustomerWowGirl = 65,
        ThiefCaughtCheer = 66,        // 도둑을 잡았을 때 머리 위치에서 환호
        // 가게 배경 음악 변형 (ShopBgm이 고름)
        BgmShopChill = 70,            // 2일차부터 BgmShop과 번갈아
        BgmShopLucky = 71,
        ThiefHurtMale = 72,           // 대걸레에 맞은 도둑 "아야" (11-20 G6, 임시: 화남 클립 음높이 올림)
        ThiefHurtFemale = 73,            // 전날 유니크 이상을 뽑은 다음 날 (기분 좋은 날)
    }

    /// <summary>
    /// 소리 칸 모음 (Resources/SoundBank). 칸에 클립을 넣기만 하면 그 순간에 납니다 — 코드 · 씬 연결 불필요.
    /// 칸이 비어 있으면 조용히 넘어갑니다. 클립을 여러 개 넣으면 무작위로 하나 (같은 소리 반복 피로 줄이기).
    /// 만들기: Tools > CardShop > Sound > Create Sound Bank
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SoundId id;
            [Tooltip("여러 개면 무작위로 하나")]
            public AudioClip[] clips = Array.Empty<AudioClip>();
            [Range(0f, 1f)] public float volume = 0.8f;
            [Tooltip("재생할 때마다 음높이를 이 범위에서 무작위로 (1 = 원래)")]
            public Vector2 pitch = new Vector2(0.95f, 1.05f);
        }

        public List<Entry> entries = new List<Entry>();

        Dictionary<SoundId, Entry> map;

        public Entry Get(SoundId id)
        {
            if (map == null || map.Count != entries.Count)
            {
                map = new Dictionary<SoundId, Entry>();
                foreach (Entry e in entries) if (e != null) map[e.id] = e;
            }
            return map.TryGetValue(id, out Entry entry) ? entry : null;
        }

        /// <summary>BgmPlayer가 반복 재생하는 칸 (배경 음악 · 환경음)</summary>
        public static bool IsLoop(SoundId id) =>
            (id >= SoundId.BgmTitle && id <= SoundId.AmbienceStreet) || id == SoundId.BgmShopChill || id == SoundId.BgmShopLucky;

        /// <summary>칸이 모두 있는지 맞춤 (새 SoundId가 생기면 빈 칸 추가). 에디터 도구가 부름.</summary>
        public int EnsureAllEntries()
        {
            int added = 0;
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                if (entries.Exists(e => e != null && e.id == id)) continue;
                bool music = IsLoop(id);
                entries.Add(new Entry { id = id, volume = music ? 0.35f : 0.8f, pitch = music ? Vector2.one : new Vector2(0.95f, 1.05f) });
                added++;
            }
            entries.Sort((a, b) => a.id.CompareTo(b.id));
            map = null;
            return added;
        }
    }
}
