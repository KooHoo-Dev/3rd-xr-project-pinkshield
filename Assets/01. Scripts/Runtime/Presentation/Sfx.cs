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
            return e != null && e.clips != null && e.clips.Length > 0;
        }

        public static void Play(SoundId id, Vector3 position) => PlayAt(id, position, 1f);

        /// <summary>
        /// 후보 칸 중 처음으로 소리가 든 칸에서, pick 번째 클립을 냅니다 (손님마다 정한 번호 → 같은 손님은 같은 목소리).
        /// 모든 칸이 비면 조용히 넘어갑니다.
        /// </summary>
        public static void PlayFirst(SoundId[] candidates, Vector3 position, int pick)
        {
            foreach (SoundId id in candidates)
            {
                if (!Has(id)) continue;
                PlayAt(id, position, 1f, pick);
                return;
            }
        }

        /// <summary>머리 자리에서 (UI · 혼잣말 같은 소리).</summary>
        public static void Play(SoundId id)
        {
            Camera cam = Camera.main;
            PlayAt(id, cam != null ? cam.transform.position : Vector3.zero, 0f);
        }

        /// <param name="pick">0 이상이면 그 번째 클립 (클립 수로 나눈 나머지), 음수면 무작위</param>
        static void PlayAt(SoundId id, Vector3 position, float spatial, int pick = -1)
        {
            SoundBank.Entry e = Bank != null ? Bank.Get(id) : null;
            if (e == null || e.clips == null || e.clips.Length == 0) return;
            int slot = (int)id % lastPlayed.Length;
            if (Time.unscaledTime - lastPlayed[slot] < MinGap) return;
            lastPlayed[slot] = Time.unscaledTime;

            AudioClip clip = e.clips[pick >= 0 ? pick % e.clips.Length : Random.Range(0, e.clips.Length)];
            if (clip == null) return;
            // PlayClipAtPoint는 음높이를 못 바꿔서 짧게 쓰고 버리는 AudioSource를 만듦
            var go = new GameObject("Sfx_" + id);
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = e.volume * AudioVolume.Effects; // 타이틀 판 [효과음] 크기 (11-20 H3)
            src.pitch = Random.Range(Mathf.Min(e.pitch.x, e.pitch.y), Mathf.Max(e.pitch.x, e.pitch.y));
            src.spatialBlend = spatial;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.maxDistance = 25f;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, src.pitch) + 0.1f);
        }
    }
}
