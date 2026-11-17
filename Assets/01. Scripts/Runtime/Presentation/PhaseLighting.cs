using CardShop.Core;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 페이즈별 조명 분위기 (시연 피드백: 하루의 흐름·쉬는 느낌).
    /// 영업 = 낮 / 정산·퇴근 전 = 노을빛 / 집 = 어둡고 따뜻하게. 바뀌는 시점은 대부분 장면 전환(검은 화면) 중입니다.
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
            Color color = dayColor;
            float intensity = dayIntensity;
            Color ambient = dayAmbient;
            switch (phase)
            {
                case GamePhaseId.Settlement:
                case GamePhaseId.AfterHours:
                    color = eveningColor; intensity = eveningIntensity; ambient = eveningAmbient; break;
                case GamePhaseId.Home:
                    color = homeColor; intensity = homeIntensity; ambient = homeAmbient; break;
            }
            if (sun != null) { sun.color = color; sun.intensity = intensity; }
            if (driveAmbient)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = ambient;
            }
            if (homeLight != null) homeLight.SetActive(phase == GamePhaseId.Home);
        }
    }
}
