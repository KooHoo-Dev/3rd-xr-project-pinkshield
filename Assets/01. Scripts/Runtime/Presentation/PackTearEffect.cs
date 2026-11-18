using System.Collections;
using System.Collections.Generic;
using CardShop.Cards;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 팩 뜯김 연출 (시연 피드백 A4). 개봉 판정이 끝난 뒤 보여 주기만 합니다(건너뛰어도 데이터는 같음).
    /// ① 윗 압착 띠(SealTop + 톱니 줄 + 접힘 모서리 FoldHinge)가 떨어져 나가 위로 튀었다가 떨어지며 작아져 사라짐
    /// ② 빈 팩이 아래로 빠지며 사라짐. 그동안 CardRevealer가 카드 묶음을 위로 올립니다.
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
        [SerializeField, Range(0f, 1f)] float tearVolume = 0.8f;

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

        /// <summary>
        /// 연출을 시작하고, 카드 묶음이 솟기 시작해야 할 시점(초)을 돌려줍니다.
        /// 등급을 미리 드러내는 빛줄기 · 특수 팩 예고는 없습니다 (DEC-004 수정).
        /// </summary>
        public float Play()
        {
            if (playing) return cardsRiseDelay;
            playing = true;
            if (tearSound != null) AudioSource.PlayClipAtPoint(tearSound, transform.position, tearVolume);
            else Sfx.Play(SoundId.PackTear, transform.position); // 인스펙터 칸이 비면 SoundBank
            StartCoroutine(Run());
            return cardsRiseDelay;
        }

        IEnumerator Run()
        {
            Transform strip = DetachTopStrip();

            Vector3 startPos = transform.position;
            Vector3 startScale = transform.localScale;
            Quaternion startRot = transform.rotation;
            Vector3 stripStart = strip != null ? strip.position : Vector3.zero;
            Quaternion stripRot = strip != null ? strip.rotation : Quaternion.identity;
            Vector3 up = transform.up, right = transform.right, fwd = transform.forward;
            float size = transform.lossyScale.y; // 팩이 눈앞에서 커져 있으면 움직임도 같이 크게

            float end = Mathf.Max(stripSeconds, cardsRiseDelay + packDropSeconds);
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

                // ② 빈 팩: 점점 빨라지며 아래로, 후반에 작아짐
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
        Transform DetachTopStrip()
        {
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

            var pivot = new GameObject("TornStrip").transform;
            pivot.SetParent(visual, false);
            pivot.localPosition = seal != null ? seal.localPosition : Vector3.zero;
            foreach (Transform p in parts) p.SetParent(pivot, true);
            return pivot;
        }
    }
}
