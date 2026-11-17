using System.Collections.Generic;
using CardShop.Customers;
using UnityEngine;

namespace CardShop.Decor
{
    /// <summary>
    /// 입구 종 (인테리어 설계 S8): 손님이 출입구 선을 지날 때 "딩-동". 플레이어가 진열장 쪽을 보고 있어도 손님이 온 걸 소리로 압니다.
    /// 소리 파일 없이 사인파 두 음을 만들어 씁니다 (나중에 clip에 효과음을 넣으면 그걸 씀).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class DoorBell : MonoBehaviour
    {
        [Tooltip("비우면 시작할 때 씬에서 찾음 (인테리어 프리팹은 씬 오브젝트를 참조할 수 없음)")]
        [SerializeField] CustomerDirector director;
        [Tooltip("비우면 코드로 만든 두 음 종소리")]
        [SerializeField] AudioClip clip;
        [SerializeField, Range(0f, 1f)] float volume = 0.5f;

        AudioSource source;
        readonly Dictionary<CustomerAgent, bool> side = new Dictionary<CustomerAgent, bool>();
        readonly List<CustomerAgent> gone = new List<CustomerAgent>();
        static AudioClip generated;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = 15f;
            if (clip == null) clip = generated ??= MakeChime();
        }

        void Start()
        {
            if (director == null) director = FindAnyObjectByType<CustomerDirector>();
        }

        void Update()
        {
            if (director == null || director.Door == null) return;
            float doorZ = director.Door.position.z + 0.35f; // 문 안쪽 지점보다 조금 바깥 = 실제 문틀 근처

            foreach (CustomerAgent agent in director.Agents)
            {
                if (agent == null) continue;
                bool outside = agent.transform.position.z > doorZ;
                if (side.TryGetValue(agent, out bool was) && was != outside) Ring();
                side[agent] = outside;
            }

            // 떠난 손님 정리
            gone.Clear();
            foreach (CustomerAgent a in side.Keys) if (a == null) gone.Add(a);
            foreach (CustomerAgent a in gone) side.Remove(a);
        }

        public void Ring()
        {
            if (clip != null) source.PlayOneShot(clip, volume);
        }

        // 두 음(E6 → C6) 종소리, 각 음은 지수 감쇠 + 배음 조금
        static AudioClip MakeChime()
        {
            const int rate = 44100;
            const float length = 1.4f;
            int n = (int)(rate * length);
            var data = new float[n];
            AddTone(data, rate, 1318.5f, 0f, 0.55f);
            AddTone(data, rate, 1046.5f, 0.32f, 0.5f);
            var c = AudioClip.Create("DoorChime", n, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }

        static void AddTone(float[] data, int rate, float freq, float start, float amp)
        {
            int s0 = (int)(start * rate);
            for (int i = s0; i < data.Length; i++)
            {
                float t = (i - s0) / (float)rate;
                float env = Mathf.Exp(-t * 4.5f) * Mathf.Clamp01(t * 400f); // 딸깍 소리 방지용 짧은 시작
                float w = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t);
                data[i] += w * env * amp * 0.5f;
            }
        }
    }
}
