using CardShop.Cards;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 효과음 한 줄 재생: <c>Sfx.Play(SoundId.PosPay, 위치)</c>. 소리는 Resources/SoundBank 에셋의 칸에서 고릅니다.
    /// 칸이 비었거나 에셋이 없으면 아무 일도 하지 않으므로, 소리 파일이 없는 지금도 부르는 곳을 미리 넣어 둘 수 있습니다.
    /// 위치가 있으면 3D(그 자리에서 들림), 없으면 머리 위치에서 2D처럼.
    /// 같은 소리가 한 프레임에 여러 번 불리면(건너뛰기 등) 한 번만 냅니다.
    /// </summary>
    public static class Sfx
    {
        const float MinGap = 0.03f;
        static SoundBank bank;
        static bool loaded;
        static readonly float[] lastPlayed = new float[128]; // SoundId 값으로 바로 찾음 (가장 큰 값보다 크게)
        static readonly int[] lastClip = NewLastClip(); // 칸마다 바로 전에 낸 클립 번호 (무작위로 고를 때 같은 클립 연달아 피하기)

        public static SoundBank Bank
        {
            get
            {
                if (!loaded)
                {
                    bank = Resources.Load<SoundBank>("SoundBank");
                    loaded = true;
                }
                return bank;
            }
        }

        /// <summary>이 칸에 소리가 들어 있나 (없으면 대신 다른 소리를 내는 곳이 있음: 스캐너 삑 · 입구 종).</summary>
        public static bool Has(SoundId id)
        {
            SoundBank.Entry e = Bank != null ? Bank.Get(id) : null;
            return e != null && e.clips != null && e.clips.Length > 0 && FirstClip(e) != null; // 지운 파일 자리만 남은 칸은 빈 칸
        }

        public static void Play(SoundId id, Vector3 position) => PlayAt(id, position, 1f);

        /// <param name="spatial">0 = 머리에서 듣는 것처럼 · 1 = 그 자리에서만 (여러 사람 환호처럼 넓게 퍼지는 소리는 중간)</param>
        public static void Play(SoundId id, Vector3 position, float spatial) => PlayAt(id, position, Mathf.Clamp01(spatial));

        /// <summary>크기를 줄여서 (부딪힌 세기 같은 것, 0~1)</summary>
        public static void PlayScaled(SoundId id, Vector3 position, float volumeScale) => PlayAt(id, position, 1f, -1, Mathf.Clamp01(volumeScale));

        /// <summary>
        /// 후보 칸 중 처음으로 소리가 든 칸에서 클립 하나를 냅니다 (pick이 음수면 무작위 · 바로 전 클립은 피함).
        /// 모든 칸이 비면 조용히 넘어갑니다.
        /// </summary>
        /// <returns>낸 칸과 클립 이름 (기록용). 아무것도 안 냈으면 null</returns>
        public static string PlayFirst(SoundId[] candidates, Vector3 position, int pick = -1)
        {
            foreach (SoundId id in candidates)
            {
                if (!Has(id)) continue;
                AudioClip clip = PlayAt(id, position, 1f, pick);
                return clip != null ? $"{id}/{clip.name}" : null;
            }
            return null;
        }

        /// <summary>머리 자리에서 (UI · 혼잣말 같은 소리).</summary>
        public static void Play(SoundId id)
        {
            Camera cam = Camera.main;
            PlayAt(id, cam != null ? cam.transform.position : Vector3.zero, 0f);
        }

        /// <param name="pick">0 이상이면 그 번째 클립 (클립 수로 나눈 나머지), 음수면 무작위 (클립이 둘 이상이면 바로 전 클립은 피함)</param>
        static AudioClip PlayAt(SoundId id, Vector3 position, float spatial, int pick = -1, float volumeScale = 1f)
        {
            SoundBank.Entry e = Bank != null ? Bank.Get(id) : null;
            if (e == null || e.clips == null || e.clips.Length == 0) return null;
            int slot = (int)id % lastPlayed.Length;
            if (Time.unscaledTime - lastPlayed[slot] < MinGap) return null;
            lastPlayed[slot] = Time.unscaledTime;

            int index = pick >= 0 ? pick % e.clips.Length : PickNoRepeat(e.clips.Length, lastClip[slot]);
            lastClip[slot] = index;
            AudioClip clip = e.clips[index];
            if (clip == null) clip = FirstClip(e); // 지운 파일 자리(빈 칸)가 섞여 있어도 남은 클립으로 (11-21 2차 S0)
            if (clip == null) return null;
            // PlayClipAtPoint는 음높이를 못 바꿔서 짧게 쓰고 버리는 AudioSource를 만듦
            var go = new GameObject("Sfx_" + id);
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = e.volume * volumeScale * AudioVolume.Effects; // 타이틀 판 [효과음] 크기 (11-20 H3)
            src.pitch = Random.Range(Mathf.Min(e.pitch.x, e.pitch.y), Mathf.Max(e.pitch.x, e.pitch.y));
            src.spatialBlend = spatial;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.maxDistance = 25f;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, src.pitch) + 0.1f);
            return clip;
        }

        /// <summary>
        /// 이어서 나는 소리용 (대걸레 문지르기): 그 칸의 클립 하나를 반복 재생하는 AudioSource를 owner에 붙여 돌려줌.
        /// 크기는 부르는 쪽이 매 프레임 정합니다(0이면 조용). 칸이 비면 null.
        /// </summary>
        public static AudioSource CreateLoop(SoundId id, GameObject owner)
        {
            SoundBank.Entry e = Bank != null ? Bank.Get(id) : null;
            if (e == null || e.clips == null || e.clips.Length == 0 || owner == null) return null;
            AudioClip clip = e.clips[Random.Range(0, e.clips.Length)];
            if (clip == null) clip = FirstClip(e);
            if (clip == null) return null;
            var src = owner.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.volume = 0f;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.maxDistance = 25f;
            return src;
        }

        /// <summary>0 ~ count-1 무작위, 둘 이상이면 last와 다른 번호 (나머지 중에서 고르게)</summary>
        static int PickNoRepeat(int count, int last)
        {
            if (count <= 1 || last < 0 || last >= count) return Random.Range(0, count);
            return (last + 1 + Random.Range(0, count - 1)) % count;
        }

        static int[] NewLastClip()
        {
            var a = new int[128];
            for (int i = 0; i < a.Length; i++) a[i] = -1;
            return a;
        }

        static AudioClip FirstClip(SoundBank.Entry e)
        {
            foreach (AudioClip c in e.clips) if (c != null) return c;
            return null;
        }

        /// <summary>CreateLoop 소리의 칸 크기 × 효과음 크기 (부르는 쪽이 0~1을 곱함)</summary>
        public static float LoopVolume(SoundId id)
        {
            SoundBank.Entry e = Bank != null ? Bank.Get(id) : null;
            return (e != null ? e.volume : 0f) * AudioVolume.Effects;
        }
    }
}
