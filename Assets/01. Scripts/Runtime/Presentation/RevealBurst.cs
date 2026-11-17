using System.Collections;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 레전더리 · 갓팩 전용 반짝임 (개봉연출 설계 3장 "파티클 소량"). Quest 성능을 위해 ParticleSystem 대신
    /// 작은 판 14장을 스크립트로 한 번만 퍼뜨리고 사라집니다(루프 없음). 오브젝트 하나를 계속 재사용합니다.
    /// 카드 앞면을 가리지 않도록 CardRevealer가 카드 조금 뒤쪽에 놓습니다.
    /// </summary>
    public sealed class RevealBurst : MonoBehaviour
    {
        const int Count = 14;
        const float BitSize = 0.012f;

        SpriteRenderer[] bits;
        Coroutine routine;

        public static RevealBurst Create(Transform parent)
        {
            var go = new GameObject("RevealBurst");
            go.transform.SetParent(parent, false);
            var burst = go.AddComponent<RevealBurst>();
            burst.bits = new SpriteRenderer[Count];
            for (int i = 0; i < Count; i++)
            {
                burst.bits[i] = RevealSprites.Quad(go.transform, $"Bit{i}", RevealSprites.Center, Vector3.zero, Vector2.one * BitSize, Color.clear, 2);
                burst.bits[i].transform.localRotation = Quaternion.Euler(0f, 0f, 45f); // 마름모
            }
            go.SetActive(false);
            return burst;
        }

        /// <param name="facing">판이 퍼지는 면의 방향 (카드와 같은 회전)</param>
        /// <param name="radius">퍼지는 최대 거리 (m)</param>
        public void Play(Vector3 position, Quaternion facing, Color color, float radius, float seconds)
        {
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(position, facing);
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run(color, radius, Mathf.Max(0.1f, seconds)));
        }

        public void Stop()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            gameObject.SetActive(false);
        }

        IEnumerator Run(Color color, float radius, float seconds)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                float spread = 1f - (1f - t) * (1f - t); // 빠르게 퍼졌다가 느려짐
                for (int i = 0; i < Count; i++)
                {
                    // 순번마다 정해진 방향·거리 (난수 없음, 매번 같은 모양)
                    float golden = i * 0.618034f;
                    golden -= Mathf.Floor(golden);
                    float angle = (i / (float)Count + golden * 0.08f) * Mathf.PI * 2f;
                    float dist = radius * (0.55f + 0.45f * golden) * spread;
                    Transform b = bits[i].transform;
                    b.localPosition = new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);
                    b.localScale = Vector3.one * (BitSize * (1.2f - t));
                    Color c = color;
                    c.a = 1f - t * t;
                    bits[i].color = c;
                }
                yield return null;
            }
            routine = null;
            gameObject.SetActive(false);
        }
    }
}
