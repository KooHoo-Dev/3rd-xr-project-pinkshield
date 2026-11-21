using System.Collections;
using CardShop.UI;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 좋은 카드 · 팩이 나올 때 퍼지는 슬라임 방울 폭죽 (개봉연출 설계 3장, 폴리싱 설계 P2 ③). Quest 성능을 위해 ParticleSystem 대신
    /// 작은 판을 스크립트로 한 번만 퍼뜨리고 사라집니다(루프 없음). 오브젝트 하나를 계속 재사용합니다.
    /// 방울은 카드 문양과 같은 슬라임 돔 모양이고, 세 개 중 하나는 플레이어 쪽으로 튀어나오며 커져 입체감을 줍니다.
    /// 카드 앞면을 가리지 않도록 부르는 쪽이 카드 조금 뒤쪽에 놓습니다.
    /// </summary>
    public sealed class RevealBurst : MonoBehaviour
    {
        const int MaxCount = 20;
        const float BitSize = 0.012f;
        const float PopOutDepth = 0.05f; // 앞으로 튀어나오는 방울이 가는 깊이 (m, 플레이어 쪽)

        SpriteRenderer[] bits;
        Coroutine routine;

        public static RevealBurst Create(Transform parent)
        {
            var go = new GameObject("RevealBurst");
            go.transform.SetParent(parent, false);
            var burst = go.AddComponent<RevealBurst>();
            burst.bits = new SpriteRenderer[MaxCount];
            for (int i = 0; i < MaxCount; i++)
                burst.bits[i] = RevealSprites.Quad(go.transform, $"Bit{i}", UiSprites.Dome(), Vector3.zero, Vector2.one * BitSize, Color.clear, 2);
            go.SetActive(false);
            return burst;
        }

        /// <summary>한 가지 색, 방울 14개 (상자 장면 · 갓팩 예고).</summary>
        public void Play(Vector3 position, Quaternion facing, Color color, float radius, float seconds) =>
            Play(position, facing, new[] { color }, radius, seconds, 14, 0f);

        /// <param name="facing">판이 퍼지는 면의 방향 (카드와 같은 회전, 카드 forward = 플레이어 반대쪽)</param>
        /// <param name="palette">방울 색. 여러 개면 순서대로 돌려 씀 (레전더리 무지개)</param>
        /// <param name="radius">퍼지는 최대 거리 (m)</param>
        /// <param name="count">방울 수 (최대 20)</param>
        /// <param name="delay">시작 전 기다림 (초, 겹 폭죽)</param>
        public void Play(Vector3 position, Quaternion facing, Color[] palette, float radius, float seconds, int count, float delay)
        {
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(position, facing);
            if (routine != null) StopCoroutine(routine);
            foreach (SpriteRenderer b in bits) b.color = Color.clear;
            routine = StartCoroutine(Run(palette, radius, Mathf.Max(0.1f, seconds), Mathf.Clamp(count, 1, MaxCount), delay));
        }

        public void Stop()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            gameObject.SetActive(false);
        }

        IEnumerator Run(Color[] palette, float radius, float seconds, int count, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float spin = Random.value * Mathf.PI * 2f; // 겹마다 방향이 조금씩 달라 보이게
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                float spread = 1f - (1f - t) * (1f - t); // 빠르게 퍼졌다가 느려짐
                for (int i = 0; i < count; i++)
                {
                    float golden = i * 0.618034f;
                    golden -= Mathf.Floor(golden);
                    float angle = spin + (i / (float)count + golden * 0.08f) * Mathf.PI * 2f;
                    float dist = radius * (0.75f + 0.25f * golden) * spread;
                    bool popOut = i % 3 == 0;
                    Transform b = bits[i].transform;
                    // 아래로 살짝 처지며(젤리 무게) 퍼짐. 앞으로 튀는 방울은 -Z(플레이어 쪽)로
                    b.localPosition = new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist - radius * 0.12f * t * t,
                        popOut ? -PopOutDepth * spread : 0f);
                    float size = popOut ? Mathf.Lerp(0.8f, 1.6f, t) : 1.2f - t * 0.6f;
                    b.localScale = Vector3.one * (BitSize * size / bits[i].sprite.bounds.size.x); // 돔 그림이 4:3이라 높이는 저절로 0.75배
                    Color c = palette[i % palette.Length];
                    c.a = Mathf.Clamp01(t * 10f) * (1f - t * t);
                    bits[i].color = c;
                }
                yield return null;
            }
            routine = null;
            gameObject.SetActive(false);
        }
    }
}
