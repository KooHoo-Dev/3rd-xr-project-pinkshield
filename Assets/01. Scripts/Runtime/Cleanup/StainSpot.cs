using CardShop.Cards;
using CardShop.Customers;
using CardShop.Logic;
using CardShop.Presentation;
using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 바닥 얼룩 (빌드피드백_11-19_FIX설계 F5, DEC-030). 대걸레(Broom + MopStation) 머리를 대고 문지르면 옅어지고 작아지다 사라집니다.
    /// 모양은 바닥에 눕힌 SpriteRenderer(코드로 만든 둥근 얼룩, WorldSign과 같은 방식) — URP 데칼은 Quest 부담 때문에 안 씀.
    /// 판정은 StainRules(순수, 테스트)가 하고, 이 클래스는 매 프레임 대걸레 머리를 넘겨주고 보이는 것만 바꿉니다.
    /// </summary>
    public sealed class StainSpot : CleanupSpot
    {
        const float ScrubRequired = 1.2f;   // 다 지우는 데 문지를 거리 (m, 약 2초)
        const float FloorLift = 0.004f;     // 바닥과 겹쳐 깜빡이지 않게
        const float MinShrink = 0.6f;       // 거의 다 지웠을 때 크기 비율
        const float SoundGap = 0.35f;       // 쓱쓱 소리 간격 (초)
        const float SlipPatienceSeconds = 3f; // 얼룩을 밟은 줄 손님 인내 시간 감소 (F6 ①)
        static readonly string[] SlipLines = { "으, 미끄러워!", "어머, 바닥이 왜 이래", "끈적끈적…" };
        static readonly Color[] Colors =
        {
            new Color(0.36f, 0.22f, 0.12f, 0.85f), // 커피
            new Color(0.32f, 0.27f, 0.2f, 0.85f),  // 흙탕
            new Color(0.55f, 0.18f, 0.32f, 0.8f),  // 탄산음료
        };
        static Sprite blob;

        Broom mop;
        MopStation station;
        SpriteRenderer look;
        Color baseColor;
        float radius, remaining = 1f, nextSound;
        Vector3 lastHead;
        bool hasLast;

        /// <summary>밟았는지 볼 손님들 (CustomerDirector.Agents, CleanupDirector가 넣음). 없으면 밟기 없음</summary>
        public IReadOnlyList<CustomerAgent> Customers { get; set; }
        readonly HashSet<CustomerAgent> slipped = new HashSet<CustomerAgent>();

        /// <summary>일시정지 중이면 true (CleanupDirector가 넣음)</summary>
        public System.Func<bool> IsPaused { get; set; }

        public static StainSpot Create(Transform parent, Vector3 floorPos, float diameter, Broom mop, IRandomSource random)
        {
            var go = new GameObject("FloorStain");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(floorPos + Vector3.up * FloorLift,
                Quaternion.Euler(90f, (float)random.NextDouble() * 360f, 0f)); // 눕힘: 스프라이트 앞면이 위
            go.transform.localScale = Vector3.one * diameter;
            var spot = go.AddComponent<StainSpot>();
            spot.mop = mop;
            spot.station = mop != null ? mop.GetComponent<MopStation>() : null;
            spot.radius = diameter * 0.5f;
            spot.look = go.AddComponent<SpriteRenderer>();
            spot.look.sprite = Blob();
            spot.baseColor = Colors[random.Range(0, Colors.Length)];
            spot.look.color = spot.baseColor;
            return spot;
        }

        void Update()
        {
            if (!IsDone && (IsPaused == null || !IsPaused())) CheckSlips();
            if (IsDone || mop == null || !mop.IsHeld || (IsPaused != null && IsPaused())) { hasLast = false; return; }
            Vector3 head = mop.HeadPosition;
            float dt = Time.deltaTime;
            float scrubbed = hasLast ? StainRules.Scrubbed(lastHead, head, dt, transform.position, radius, transform.position.y - FloorLift) : 0f;
            if (scrubbed > 0f) Scrub(scrubbed, (head - lastHead) / dt);
            lastHead = head;
            hasLast = true;
        }

        // 얼룩 위를 지나는 손님(쏟고 나가는 손님은 빼고)은 한 번 "미끄러워!" + 줄에 서 있으면 인내 시간 −3초 (F6 ①)
        void CheckSlips()
        {
            if (Customers == null) return;
            foreach (CustomerAgent a in Customers)
            {
                if (a == null || a.State == CustomerState.Leave || slipped.Contains(a)) continue;
                Vector3 d = a.transform.position - transform.position;
                if (d.x * d.x + d.z * d.z > radius * radius) continue;
                slipped.Add(a);
                if (a.Bubble != null) a.Bubble.Say(SlipLines[Random.Range(0, SlipLines.Length)], 2f);
                if (a.State == CustomerState.Queue) a.QueueSeconds += SlipPatienceSeconds;
            }
        }

        void Scrub(float scrubbed, Vector3 velocity)
        {
            remaining = StainRules.Remaining(remaining, scrubbed, ScrubRequired);
            Color c = baseColor;
            c.a *= remaining;
            look.color = c;
            transform.localScale = Vector3.one * (radius * 2f * Mathf.Lerp(MinShrink, 1f, remaining)); // 가장자리부터 작아짐
            if (station != null) station.Scrub(velocity);
            if (Time.time >= nextSound) { Sfx.Play(SoundId.MopScrub, transform.position); nextSound = Time.time + SoundGap; }
            if (remaining > 0f) return;

            Sfx.Play(SoundId.StainClean, transform.position);
            gameObject.SetActive(false);
            Complete();
        }

        // 가운데가 진하고 가장자리가 울퉁불퉁 옅어지는 둥근 얼룩 (한 번만 만듦)
        static Sprite Blob()
        {
            if (blob != null) return blob;
            const int px = 64;
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[px * px];
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                {
                    float dx = (x + 0.5f) / px * 2f - 1f, dy = (y + 0.5f) / px * 2f - 1f;
                    float angle = Mathf.Atan2(dy, dx);
                    float edge = 0.78f + 0.08f * Mathf.Sin(angle * 3f) + 0.05f * Mathf.Sin(angle * 7f + 1.3f); // 울퉁불퉁한 테두리
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / edge;
                    float a = Mathf.Clamp01((1f - d) * 6f) * Mathf.Lerp(1f, 0.75f, d);
                    pixels[y * px + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            blob = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px); // 1 유닛 = 지름 1m (크기는 localScale)
            return blob;
        }
    }
}
