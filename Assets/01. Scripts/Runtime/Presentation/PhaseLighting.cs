using CardShop.Core;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 페이즈별 조명 분위기 (시연 피드백: 하루의 흐름·쉬는 느낌).
    /// 영업 = 낮 / 타이틀·정산·퇴근 전 = 노을빛 / 집 = 어둡고 따뜻하게. 바뀌는 시점은 대부분 장면 전환(검은 화면) 중입니다.
    /// </summary>
    public sealed class PhaseLighting : MonoBehaviour
    {
        [SerializeField] Light sun;
        [Tooltip("집에서만 켜지는 따뜻한 조명")]
        [SerializeField] GameObject homeLight;
        [SerializeField] Color dayColor = Color.white;
        [SerializeField] float dayIntensity = 1.1f;
        [SerializeField] Color eveningColor = new Color(1f, 0.72f, 0.5f);
        [SerializeField] float eveningIntensity = 0.85f;
        [SerializeField] Color homeColor = new Color(1f, 0.8f, 0.6f);
        [SerializeField] float homeIntensity = 0.25f;

        [Header("환경광 (천장이 해를 막아 실내는 대부분 환경광으로 밝힘)")]
        [Tooltip("끄면 RenderSettings 환경광을 건드리지 않음")]
        [SerializeField] bool driveAmbient = true;
        [SerializeField] Color dayAmbient = new Color(0.62f, 0.6f, 0.58f);
        [SerializeField] Color eveningAmbient = new Color(0.5f, 0.42f, 0.38f);
        [SerializeField] Color homeAmbient = new Color(0.42f, 0.36f, 0.32f);

        GameContext subscribed;
        GamePhaseId current = GamePhaseId.Title;
        float multiplier = 1f;

        /// <summary>
        /// 밝기 배수 (1 = 그대로). 튜토리얼 정지 중 가게를 살짝 어둡게 해 대상 하이라이트를 돋보이게 합니다 (튜토리얼_설계 5-3).
        /// 페이즈 조명에 곱하므로 페이즈가 바뀌어도 배수는 유지됩니다.
        /// </summary>
        public float Multiplier
        {
            get => multiplier;
            set
            {
                value = Mathf.Clamp(value, 0f, 2f);
                if (Mathf.Approximately(value, multiplier)) return;
                multiplier = value;
                Apply(current);
            }
        }

        void OnEnable()
        {
            subscribed = GameContext.Instance;
            if (subscribed != null) subscribed.Events.PhaseChanged += OnPhaseChanged;
            Apply(GamePhaseId.Title);
        }

        void OnDisable()
        {
            if (subscribed != null) subscribed.Events.PhaseChanged -= OnPhaseChanged;
            subscribed = null;
        }

        void OnPhaseChanged(GamePhaseId from, GamePhaseId to) => Apply(to);

        void Apply(GamePhaseId phase)
        {
            current = phase;
            Color color = dayColor;
            float intensity = dayIntensity;
            Color ambient = dayAmbient;
            switch (phase)
            {
                case GamePhaseId.Title: // 가게 앞 인도의 타이틀은 노을빛 (타이틀연출_손님_설계 0-2)
                case GamePhaseId.Settlement:
                case GamePhaseId.AfterHours:
                    color = eveningColor; intensity = eveningIntensity; ambient = eveningAmbient; break;
                case GamePhaseId.Home:
                    color = homeColor; intensity = homeIntensity; ambient = homeAmbient; break;
            }
            if (sun != null) { sun.color = color; sun.intensity = intensity * multiplier; }
            if (driveAmbient)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = ambient * multiplier;
            }
            if (homeLight != null) homeLight.SetActive(phase == GamePhaseId.Home);
        }
    }
}
