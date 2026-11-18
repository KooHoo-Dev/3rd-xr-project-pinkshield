using CardShop.Presentation;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 정지 중 가게 조명을 살짝 낮춥니다 (튜토리얼_설계 5-3: 화면을 덮는 딤 대신 조명만, 멀미 · Quest 성능 때문).
    /// PhaseLighting.Multiplier를 부드럽게 바꾸므로 페이즈 조명(낮 · 노을 · 집)과 충돌하지 않습니다. 기다림 · 말 안내에서는 낮추지 않습니다.
    /// </summary>
    public sealed class TutorialLightDim : MonoBehaviour
    {
        [Tooltip("정지 중 밝기 배수 (1 = 안 낮춤)")]
        [Range(0.3f, 1f)] [SerializeField] float dimMultiplier = 0.75f;
        [Tooltip("밝기가 바뀌는 데 걸리는 시간 (초)")]
        [SerializeField] float fadeSeconds = 0.3f;

        PhaseLighting lighting;

        /// <summary>참이면 어둡게, 거짓이면 원래대로</summary>
        public bool Dimmed { get; set; }

        void Start() => lighting = FindAnyObjectByType<PhaseLighting>();

        void Update()
        {
            if (lighting == null) return;
            float target = Dimmed ? dimMultiplier : 1f;
            float now = lighting.Multiplier;
            if (Mathf.Approximately(now, target)) return;
            float step = (1f - dimMultiplier) / Mathf.Max(0.01f, fadeSeconds) * Time.unscaledDeltaTime;
            lighting.Multiplier = Mathf.MoveTowards(now, target, step);
        }

        void OnDisable()
        {
            if (lighting != null) lighting.Multiplier = 1f; // 튜토리얼이 꺼져도 어두운 채 남지 않게
        }
    }
}
