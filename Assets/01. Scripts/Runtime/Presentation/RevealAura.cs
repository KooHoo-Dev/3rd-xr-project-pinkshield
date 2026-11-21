using System.Collections;
using CardShop.Cards;
using CardShop.UI;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 좋은 카드가 맨 앞에 보인 순간 카드 둘레에 펼쳐지는 빛 (폴리싱 설계 P2, 11-22):
    /// 뒤 후광 · 둘레 어둡게 · 회전 빛줄기 · 테 빛 · 별 반짝임 · 앞으로 퍼지는 링 · 색종이. 슬라임 방울 폭죽은 <see cref="RevealBurst"/>.
    /// 무엇을 얼마나 켤지는 <see cref="RevealTier"/>가 정합니다. HDR · 블룸이 꺼져 있어(VR 눈 편한 색) 모두 스프라이트 알파로 표현하고,
    /// 오브젝트는 처음 한 번 만들어 계속 재사용합니다(Quest 성능, 루프 없음). 카드 forward = 플레이어 반대쪽이라 -Z가 플레이어 쪽입니다.
    /// </summary>
    public sealed class RevealAura : MonoBehaviour
    {
        const int MaxRays = 16, MaxCounterRays = 10, MaxStars = 14, MaxRings = 3, MaxConfetti = 24;
        const float StarSeconds = 0.44f, RingSeconds = 0.8f;

        SpriteRenderer halo, dim, edge;
        Transform rays, counterRays;
        SpriteRenderer[] rayBits, counterBits, stars, rings, confetti;
        Vector4[] confettiPath; // x 시작, x 흔들림, 시작 시각(0~1), 회전 속도
        Coroutine routine;
        // UiSprites는 100px = 1단위라 그림마다 실제 크기가 달라서, 미터로 맞추는 배율 (1 / 그림 폭)
        static float Unit(Sprite s) => 1f / s.bounds.size.x;

        public static RevealAura Create(Transform parent)
        {
            var go = new GameObject("RevealAura");
            go.transform.SetParent(parent, false);
            var aura = go.AddComponent<RevealAura>();
            aura.Build();
            go.SetActive(false);
            return aura;
        }

        void Build()
        {
            Transform t = transform;
            dim = RevealSprites.Quad(t, "Dim", UiSprites.SoftDot(), new Vector3(0f, 0f, 0.06f), Vector2.one * (0.7f * Unit(UiSprites.SoftDot())), Color.clear, -2);
            halo = RevealSprites.Quad(t, "Halo", UiSprites.SoftDot(), new Vector3(0f, 0f, 0.02f), Vector2.one * 0.2f, Color.clear, -1);
            rays = new GameObject("Rays").transform;
            rays.SetParent(t, false);
            rays.localPosition = new Vector3(0f, 0f, 0.025f);
            counterRays = new GameObject("CounterRays").transform;
            counterRays.SetParent(t, false);
            counterRays.localPosition = new Vector3(0f, 0f, 0.024f);
            rayBits = MakeRays(rays, MaxRays);
            counterBits = MakeRays(counterRays, MaxCounterRays);

            // 카드 테 빛: 둥근 사각 빛(9-slice)을 카드보다 1.5cm씩 크게. 모서리 32px = 1.5cm
            edge = RevealSprites.Quad(t, "Edge", UiSprites.Glow(32f / 0.015f), new Vector3(0f, 0f, 0.004f), Vector2.one, Color.clear, 0);
            edge.transform.localScale = Vector3.one;
            edge.drawMode = SpriteDrawMode.Sliced;

            stars = new SpriteRenderer[MaxStars];
            for (int i = 0; i < MaxStars; i++) stars[i] = RevealSprites.Quad(t, $"Star{i}", UiSprites.Sparkle(), Vector3.zero, Vector2.one * 0.02f, Color.clear, 3);
            rings = new SpriteRenderer[MaxRings];
            for (int i = 0; i < MaxRings; i++) rings[i] = RevealSprites.Quad(t, $"Ring{i}", UiSprites.Ring(6), Vector3.zero, Vector2.one * 0.07f, Color.clear, 3);
            confetti = new SpriteRenderer[MaxConfetti];
            confettiPath = new Vector4[MaxConfetti];
            for (int i = 0; i < MaxConfetti; i++)
                confetti[i] = RevealSprites.Quad(t, $"Confetti{i}", i % 2 == 0 ? UiSprites.Dome() : RevealSprites.Center, Vector3.zero, Vector2.one * 0.008f, Color.clear, 4);
        }

        static SpriteRenderer[] MakeRays(Transform parent, int count)
        {
            var bits = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var arm = new GameObject($"Arm{i}").transform; // 팔이 돌고, 빛줄기는 팔 위쪽으로 뻗음
                arm.SetParent(parent, false);
                bits[i] = RevealSprites.Quad(arm, "Ray", UiSprites.Ray(), Vector3.zero, Vector2.one, Color.clear, 0);
            }
            return bits;
        }

        /// <param name="card">카드 자리 (forward = 플레이어 반대쪽)</param>
        /// <param name="cardScale">카드 배율 (상자 하이라이트는 작게 놓일 수 있음)</param>
        public void Play(RevealTier tier, Pose card, float cardScale)
        {
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(card.position, card.rotation);
            transform.localScale = Vector3.one * Mathf.Max(0.1f, cardScale);
            if (routine != null) StopCoroutine(routine);
            ClearAll();
            routine = StartCoroutine(Run(tier));
        }

        public void Stop()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            gameObject.SetActive(false);
        }

        IEnumerator Run(RevealTier tier)
        {
            const float W = CardView.Width, H = CardView.Height;
            float seconds = Mathf.Max(0.2f, tier.Seconds);
            LayoutRays(rayBits, tier.Rays, tier.Radius * 1.4f);
            LayoutRays(counterBits, tier.CounterRays, tier.Radius * 1.0f);
            for (int i = 0; i < tier.Confetti; i++)
                confettiPath[i] = new Vector4(Random.Range(-0.26f, 0.26f), Random.Range(-0.05f, 0.05f), Random.Range(0f, 0.25f), Random.Range(-720f, 720f));

            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                float env = Smooth(t / 0.2f) * (1f - Smooth((t - 0.7f) / 0.3f)); // 들어오고 · 머물고 · 사라짐
                Color main = tier.Rainbow ? Color.HSVToRGB(Mathf.Repeat(t * 1.5f, 1f), 0.45f, 1f) : tier.Colors[0];

                // 후광 · 둘레 어둡게
                float breathe = tier.HaloBreathe ? 1f + 0.06f * Mathf.Sin(t * 14f) : 1f;
                halo.transform.localScale = Vector3.one * (Unit(halo.sprite) * H * tier.Halo * Mathf.Lerp(0.4f, 1f, Smooth(t / 0.25f)) * breathe);
                halo.color = WithAlpha(main, 0.55f * env);
                dim.color = new Color(0.04f, 0.03f, 0.07f, tier.Dim * env);

                // 빛줄기 (레전더리는 반대로 도는 한 겹 더)
                rays.localRotation = Quaternion.Euler(0f, 0f, 60f * t);
                counterRays.localRotation = Quaternion.Euler(0f, 0f, -60f * t + 9f);
                PaintRays(rayBits, tier.Rays, tier, tier.RayAlpha * env, Mathf.Lerp(0.5f, 1f, t));
                PaintRays(counterBits, tier.CounterRays, tier, tier.RayAlpha * 0.8f * env, Mathf.Lerp(0.5f, 1f, t));

                // 카드 테 빛 맥박
                edge.size = new Vector2(W + 0.03f, H + 0.03f);
                float pulse = tier.EdgePulses > 0 ? Mathf.Sin(Mathf.PI * Mathf.Repeat(t * tier.EdgePulses, 1f)) : 0f;
                edge.color = WithAlpha(main, 0.75f * pulse * (tier.EdgePulses > 0 ? 1f : 0f));

                // 테 둘레 별: 차례로 반짝
                for (int i = 0; i < tier.Stars; i++)
                {
                    float start = (0.1f + i * 0.65f / tier.Stars * seconds) / seconds;
                    float k = (t - start) * seconds / StarSeconds;
                    float a = Mathf.PI * 2f * i / tier.Stars;
                    float r = i % 2 == 0 ? 1f : 1.12f;
                    Transform s = stars[i].transform;
                    s.localPosition = new Vector3(Mathf.Cos(a) * W * 0.62f * r, Mathf.Sin(a) * H * 0.6f * r, -0.004f);
                    float on = k > 0f && k < 1f ? Mathf.Sin(Mathf.PI * k) : 0f;
                    s.localScale = Vector3.one * (Unit(stars[i].sprite) * 0.022f * on);
                    s.localRotation = Quaternion.Euler(0f, 0f, 90f * Mathf.Clamp01(k));
                    stars[i].color = WithAlpha(tier.Rainbow ? tier.ColorAt(i) : Color.Lerp(main, Color.white, 0.6f), 0.95f * on);
                }

                // 충격파 링: 카드에서 플레이어 쪽으로 퍼짐
                for (int i = 0; i < tier.Rings; i++)
                {
                    float k = (t * seconds - 0.08f - i * 0.2f) / RingSeconds;
                    float on = k > 0f && k < 1f ? 1f : 0f;
                    float e = Smooth(Mathf.Clamp01(k));
                    Transform rt = rings[i].transform;
                    rt.localPosition = new Vector3(0f, 0f, -0.003f - 0.08f * e);
                    rt.localScale = Vector3.one * (Unit(rings[i].sprite) * W * Mathf.Lerp(0.6f, 2.6f + 0.4f * i, e));
                    rings[i].color = WithAlpha(tier.ColorAt(i * 2), 0.85f * (1f - e) * on);
                }

                // 색종이 비
                for (int i = 0; i < tier.Confetti; i++)
                {
                    Vector4 p = confettiPath[i];
                    float k = Mathf.Clamp01((t - p.z) / 0.75f);
                    Transform c = confetti[i].transform;
                    c.localPosition = new Vector3(p.x + p.y * Mathf.Sin(k * 9f + i), Mathf.Lerp(0.2f, -0.28f, k * k * 0.4f + k * 0.6f), -0.02f);
                    c.localRotation = Quaternion.Euler(0f, 0f, p.w * k);
                    float u = Unit(confetti[i].sprite);
                    c.localScale = i % 2 == 0 ? new Vector3(0.009f * u, 0.009f * u, 1f) : new Vector3(0.005f * u, 0.012f * u, 1f);
                    float a = k > 0f && k < 1f ? 0.9f * (1f - Smooth((k - 0.8f) / 0.2f)) : 0f;
                    confetti[i].color = WithAlpha(tier.Rainbow || i % 3 != 0 ? tier.ColorAt(i) : new Color(1f, 0.95f, 0.8f), a);
                }
                yield return null;
            }
            routine = null;
            gameObject.SetActive(false);
        }

        static void LayoutRays(SpriteRenderer[] bits, int count, float length)
        {
            for (int i = 0; i < count; i++)
            {
                bits[i].transform.parent.localRotation = Quaternion.Euler(0f, 0f, 360f * i / count);
                bits[i].transform.localPosition = new Vector3(0f, length * 0.5f, 0f);
                Vector2 native = bits[i].sprite.bounds.size;
                bits[i].transform.localScale = new Vector3(0.012f / native.x, length / native.y, 1f);
            }
        }

        static void PaintRays(SpriteRenderer[] bits, int count, RevealTier tier, float alpha, float grow)
        {
            for (int i = 0; i < bits.Length; i++)
            {
                if (i >= count) { bits[i].color = Color.clear; continue; }
                bits[i].transform.parent.localScale = Vector3.one * grow;
                bits[i].color = WithAlpha(tier.ColorAt(i), alpha);
            }
        }

        void ClearAll()
        {
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true)) sr.color = Color.clear;
        }

        static float Smooth(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

        static Color WithAlpha(Color c, float a)
        {
            c.a = Mathf.Clamp01(a);
            return c;
        }
    }
}
