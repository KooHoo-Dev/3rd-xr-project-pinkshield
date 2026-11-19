using System;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 음악(BGM · 거리 환경음) · 효과음(목소리 포함) 크기 (11-20 H3). 4단계(끔 · 작게 · 보통 · 크게)를 돌려 가며 고르고 기기에 저장합니다.
    /// BgmPlayer · Sfx가 칸 음량에 곱하고, 타이틀 판의 AudioVolumeButtons가 바꿉니다.
    /// </summary>
    public static class AudioVolume
    {
        public enum Channel { Music, Effects }

        static readonly float[] Levels = { 0f, 0.35f, 0.7f, 1f };
        static readonly string[] Names = { "끔", "작게", "보통", "크게" };
        const int DefaultLevel = 3;

        static readonly int[] level = { -1, -1 };

        public static event Action Changed;

        public static float Music => Levels[LevelOf(Channel.Music)];
        public static float Effects => Levels[LevelOf(Channel.Effects)];

        public static string NameOf(Channel c) => Names[LevelOf(c)];

        /// <summary>다음 단계로 (크게 다음은 끔)</summary>
        public static void Cycle(Channel c)
        {
            int next = (LevelOf(c) + 1) % Levels.Length;
            level[(int)c] = next;
            try { PlayerPrefs.SetInt(Key(c), next); PlayerPrefs.Save(); } catch { /* 저장 못 해도 이번 실행에는 적용 */ }
            Changed?.Invoke();
        }

        static int LevelOf(Channel c)
        {
            int i = (int)c;
            if (level[i] < 0)
            {
                int saved = DefaultLevel;
                try { saved = PlayerPrefs.GetInt(Key(c), DefaultLevel); } catch { }
                level[i] = Mathf.Clamp(saved, 0, Levels.Length - 1);
            }
            return level[i];
        }

        static string Key(Channel c) => "CardShop.Volume." + c;
    }
}
