using System.Collections;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 배경 음악 · 거리 환경음 (11-19 필수 사운드 목록): 페이즈마다 곡을 바꾸고 부드럽게 겹쳐 넘깁니다.
    /// 타이틀 = BgmTitle, 영업 · 퇴근 전 = 가게 곡(ShopBgm: 1일차 고정 · 이후 번갈아 · 전날 유니크 이상을 뽑은 다음 날) + 거리 환경음, 집 = BgmHome.
    /// 곡은 SoundBank 칸에서 고르고, 비어 있으면 조용합니다.
    /// 씬을 다시 빌드하지 않아도 되게, 씬이 열리면 GameContext 오브젝트에 스스로 붙습니다.
    /// </summary>
    public sealed class BgmPlayer : PhaseListener
    {
        [SerializeField] float crossfadeSeconds = 1.5f;

        AudioSource[] music;
        AudioSource ambience;
        int current;
        SoundId? playing;
        Coroutine fade;
        bool chillToday;
        SoundId? ambienceId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            GameContext ctx = FindAnyObjectByType<GameContext>();
            if (ctx != null && ctx.GetComponent<BgmPlayer>() == null) ctx.gameObject.AddComponent<BgmPlayer>();
        }

        void Awake()
        {
            music = new[] { Source(), Source() };
            ambience = Source();
            AudioVolume.Changed += ApplyVolume;
        }

        void OnDestroy() => AudioVolume.Changed -= ApplyVolume;

        // 타이틀 판에서 [음악] 크기를 바꿈 (11-20 H3). 겹쳐 넘기는 중이면 끝날 때 새 크기가 됨
        void ApplyVolume()
        {
            SetLoop(ambience, ambienceId);
            if (fade == null && music != null) music[current].volume = Assign(music[current], playing);
        }

        AudioSource Source()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        protected override void OnDayStarted(int day)
        {
            chillToday = Random.value < 0.5f; // 2일차부터 하루에 한 번 정함 (ShopBgm)
            base.OnDayStarted(day);
        }

        SoundId ShopSong()
        {
            GameSession s = Context != null ? Context.Session : null;
            int yesterday = s != null && s.Previous != null ? s.Previous.bestRarity : -1; // 전날 유니크 이상 → 기분 좋은 날
            SoundId id = s != null ? ShopBgm.Pick(s.Day, yesterday, chillToday) : SoundId.BgmShop;
            return Sfx.Has(id) ? id : SoundId.BgmShop; // 변형 칸이 비면 기본 곡
        }

        protected override void Refresh()
        {
            SoundId? song = CurrentPhase switch
            {
                GamePhaseId.Title or GamePhaseId.Final => SoundId.BgmTitle,
                GamePhaseId.Home or GamePhaseId.DayEnd => SoundId.BgmHome,
                GamePhaseId.None => null,
                _ => ShopSong(),
            };
            bool street = CurrentPhase == GamePhaseId.Title || CurrentPhase == GamePhaseId.Intro ||
                          CurrentPhase == GamePhaseId.Business || CurrentPhase == GamePhaseId.AfterHours;
            ambienceId = street ? SoundId.AmbienceStreet : (SoundId?)null;
            SetLoop(ambience, ambienceId);
            if (song == playing) return;
            playing = song;
            if (fade != null) StopCoroutine(fade);
            if (isActiveAndEnabled) fade = StartCoroutine(Crossfade(song));
        }

        // 다음 곡을 다른 AudioSource에서 키우고 지금 곡은 줄임
        IEnumerator Crossfade(SoundId? song)
        {
            AudioSource from = music[current];
            current = 1 - current;
            AudioSource to = music[current];
            float target = Assign(to, song);
            float startFrom = from.volume;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, crossfadeSeconds))
            {
                from.volume = Mathf.Lerp(startFrom, 0f, t);
                to.volume = Mathf.Lerp(0f, target, t);
                yield return null;
            }
            from.volume = 0f;
            from.Stop();
            to.volume = target;
            fade = null;
        }

        void SetLoop(AudioSource s, SoundId? id)
        {
            float v = Assign(s, id);
            s.volume = v;
        }

        // 칸의 첫 클립을 반복 재생하도록 넣고 목표 볼륨을 돌려줌 (칸이 비면 멈춤 · 0)
        static float Assign(AudioSource s, SoundId? id)
        {
            SoundBank.Entry e = id.HasValue && Sfx.Bank != null ? Sfx.Bank.Get(id.Value) : null;
            AudioClip clip = e != null && e.clips != null && e.clips.Length > 0 ? e.clips[0] : null;
            if (clip == null) { s.Stop(); s.clip = null; return 0f; }
            if (s.clip != clip) { s.clip = clip; s.Play(); }
            else if (!s.isPlaying) s.Play();
            return e.volume * AudioVolume.Music;
        }
    }
}
