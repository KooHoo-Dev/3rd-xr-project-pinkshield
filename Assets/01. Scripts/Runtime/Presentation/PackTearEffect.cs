using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 팩 뜯김 연출 (시연 피드백 A4). 개봉 판정이 끝난 뒤 보여 주기만 합니다(건너뛰어도 데이터는 같음).
    /// ① 윗 압착 띠(SealTop + 톱니 줄 + 접힘 모서리 FoldHinge)가 떨어져 나가 위로 튀었다가 떨어지며 작아져 사라짐
    /// ② 팩 틈에서 빛줄기(가장 좋은 카드의 등급 색, 노말은 흰색): 반투명 판 한 장의 알파만 바꿈 (파티클 X, Quest 성능)
    /// ③ 빈 팩이 아래로 빠지며 사라짐. 그동안 CardRevealer가 카드 묶음을 위로 올립니다.
    /// 모두 스크립트 트윈입니다(Rigidbody 물리 X, BUG-003). 팩 오브젝트는 연출이 끝나면 스스로 지웁니다.
    /// 팩 프리팹(PackPrefabBuilder)에 붙어 있으면 그 값을, 없으면 개봉 때 기본값으로 붙여 씁니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PackTearEffect : MonoBehaviour
    {
        [Header("윗 압착 띠")]
        [Tooltip("띠가 날아가 사라지기까지 (초)")]
        [SerializeField] float stripSeconds = 0.45f;
        [Tooltip("위로 튀는 높이 (m, 팩 크기 배율 반영 전)")]
        [SerializeField] float stripRise = 0.04f;
        [Tooltip("끝날 때까지 아래로 떨어지는 거리 (m)")]
        [SerializeField] float stripFall = 0.06f;
        [Tooltip("옆(오른쪽)으로 날아가는 거리 (m)")]
        [SerializeField] float stripSide = 0.05f;
        [Tooltip("날아가며 도는 각도 (도)")]
        [SerializeField] float stripSpin = 120f;

        [Header("빛줄기")]
        [SerializeField] float beamDelay = 0.05f;
        [SerializeField] float beamSeconds = 0.5f;
        [Tooltip("빛줄기 높이 (m, 팩 크기 배율 반영 전)")]
        [SerializeField] float beamHeight = 0.1f;
        [SerializeField, Range(0f, 1f)] float beamAlpha = 0.75f;

        [Header("빈 팩")]
        [Tooltip("카드 묶음이 솟기 시작하고 빈 팩이 빠지기 시작하는 시점 (초)")]
        [SerializeField] float cardsRiseDelay = 0.25f;
        [SerializeField] float packDropSeconds = 0.6f;
        [Tooltip("빈 팩이 아래로 떨어지는 거리 (m, 팩 크기 배율 반영 전)")]
        [SerializeField] float packDrop = 0.35f;
        [Tooltip("떨어지며 옆으로 밀리는 거리 (m)와 비틀리는 각도 (도)")]
        [SerializeField] float packSide = 0.06f;
        [SerializeField] float packTumble = 35f;
        [Tooltip("뜯긴 팩이 커지는 정도 (0.12 = 12%)")]
        [SerializeField] float packGrow = 0.12f;

        [Header("소리 (비워 두면 없음)")]
        [SerializeField] AudioClip tearSound;
        [Tooltip("특수 팩(갓팩 · 레전더리 확정) 전용 예고음. 비워 두면 일반 소리")]
        [SerializeField] AudioClip specialTearSound;
        [SerializeField, Range(0f, 1f)] float tearVolume = 0.8f;
        [Tooltip("특수 팩 빛줄기 높이 배율 (금빛 예고)")]
        [SerializeField] float specialBeamScale = 1.8f;

        Transform visual;
        Rigidbody body;
        bool playing;

        /// <summary>윗 압착 띠에 속한 자식 이름. PackPrefabBuilder가 만드는 이름과 같아야 합니다.</summary>
        static bool IsTopStripPart(string n) => n == "SealTop" || n.StartsWith("CrimpTop") || n == "FoldHinge";

        void Awake()
        {
            visual = transform.Find("Visual");
            body = GetComponent<Rigidbody>();
        }

        /// <summary>연출을 시작하고, 카드 묶음이 솟기 시작해야 할 시점(초)을 돌려줍니다.</summary>
        /// <param name="special">특수 팩(갓팩 · 레전더리 확정) 예고: 빛줄기가 더 길고 전용 효과음(있으면). 색은 부르는 쪽이 금빛으로 줍니다.</param>
        public float Play(Color beamColor, bool special = false)
        {
            if (playing) return cardsRiseDelay;
            playing = true;
            AudioClip clip = special && specialTearSound != null ? specialTearSound : tearSound;
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, tearVolume);
            StartCoroutine(Run(beamColor, special ? specialBeamScale : 1f));
            return cardsRiseDelay;
        }

        IEnumerator Run(Color beamColor, float beamScale)
        {
            float height = beamHeight * beamScale;
            Transform strip = DetachTopStrip(out float openingY, out float width);
            SpriteRenderer beam = null; // 빛줄기(색 판)는 등급을 미리 드러내고 흰 빈 카드처럼 보여 뺌 (DEC-004 수정)

            Vector3 startPos = transform.position;
            Vector3 startScale = transform.localScale;
            Quaternion startRot = transform.rotation;
            Vector3 stripStart = strip != null ? strip.position : Vector3.zero;
            Quaternion stripRot = strip != null ? strip.rotation : Quaternion.identity;
            Vector3 up = transform.up, right = transform.right, fwd = transform.forward;
            float size = transform.lossyScale.y; // 팩이 눈앞에서 커져 있으면 움직임도 같이 크게

            float end = Mathf.Max(stripSeconds, beamDelay + beamSeconds, cardsRiseDelay + packDropSeconds);
            for (float time = 0f; time < end; time += Time.deltaTime)
            {
                // ① 윗 띠: 위로 튀었다가 떨어지는 포물선 + 회전, 마지막 35%에서 작아짐
                if (strip != null)
                {
                    float t = Mathf.Clamp01(time / stripSeconds);
                    float y = stripRise * 4f * t * (1f - t) - stripFall * t * t;
                    strip.position = stripStart + (right * (stripSide * t) + up * y) * size;
                    strip.rotation = Quaternion.AngleAxis(-stripSpin * t, fwd) * Quaternion.AngleAxis(stripSpin * 0.35f * t, right) * stripRot;
                    strip.localScale = Vector3.one * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, t)));
                    if (t >= 1f && strip.gameObject.activeSelf) strip.gameObject.SetActive(false);
                }

                // ② 빛줄기: 아래에서 위로 자라며 밝아졌다 사라짐
                if (beam != null)
                {
                    float t = Mathf.Clamp01((time - beamDelay) / beamSeconds);
                    Color c = beamColor;
                    c.a = beamAlpha * Mathf.Sin(t * Mathf.PI);
                    beam.color = c;
                    Vector3 s = beam.transform.localScale;
                    beam.transform.localScale = new Vector3(s.x, height * Mathf.Lerp(0.3f, 1f, 1f - (1f - t) * (1f - t)), 1f);
                }

                // ③ 빈 팩: 점점 빨라지며 아래로, 후반에 작아짐
                {
                    float t = Mathf.Clamp01((time - cardsRiseDelay) / packDropSeconds);
                    // 크기는 그대로, 비틀리며 아래로 떨어져 나감 (작아지며 사라지지 않게)
                    Vector3 pos = startPos - up * (packDrop * size * t * t) + right * (packSide * size * t);
                    Quaternion rot = Quaternion.AngleAxis(-packTumble * t * t, fwd) * startRot;
                    float grow = 1f + packGrow * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 4f)); // 뜯기자마자 살짝 커졌다가 떨어짐
                    Move(pos, rot, startScale * grow);
                }
                yield return null;
            }
            Destroy(gameObject);
        }

        // Transform과 Rigidbody를 함께 (BUG-003)
        void Move(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            transform.SetPositionAndRotation(pos, rot);
            transform.localScale = scale;
            if (body != null) { body.position = pos; body.rotation = rot; }
        }

        // 윗 띠 조각들을 새 부모 하나로 모아 한꺼번에 움직입니다. 접힘 모서리(FoldHinge)도 함께 날아가 팩에 남지 않습니다.
        Transform DetachTopStrip(out float openingY, out float width)
        {
            openingY = 0f;
            width = 0.07f;
            if (visual == null) return null;

            var parts = new List<Transform>();
            Transform seal = null;
            foreach (Transform child in visual)
            {
                if (!IsTopStripPart(child.name)) continue;
                parts.Add(child);
                if (child.name == "SealTop") seal = child;
            }
            if (parts.Count == 0) return null;

            if (seal != null)
            {
                openingY = seal.localPosition.y - seal.localScale.y * 0.5f; // 띠 아래 = 팩이 열리는 틈
                width = seal.localScale.x;
            }

            var pivot = new GameObject("TornStrip").transform;
            pivot.SetParent(visual, false);
            pivot.localPosition = seal != null ? seal.localPosition : new Vector3(0f, openingY, 0f);
            foreach (Transform p in parts) p.SetParent(pivot, true);
            return pivot;
        }

        SpriteRenderer CreateBeam(Color color, float openingY, float width, float height)
        {
            if (visual == null) return null;
            color.a = 0f;
            // 양면으로 보이는 반투명 판 (상자 일괄 연출 BoxOpenEffect와 같은 스프라이트)
            return RevealSprites.Quad(visual, "TearBeam", RevealSprites.Bottom, new Vector3(0f, openingY, 0f),
                new Vector2(width * 0.9f, height * 0.3f), color);
        }
    }
}
