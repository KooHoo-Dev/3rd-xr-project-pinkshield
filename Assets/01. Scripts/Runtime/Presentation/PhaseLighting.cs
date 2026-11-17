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
            switch (phase)
            {
                case GamePhaseId.Settlement:
                case GamePhaseId.AfterHours:
                    color = eveningColor; intensity = eveningIntensity; break;
                case GamePhaseId.Home:
                    color = homeColor; intensity = homeIntensity; break;
            }
            if (sun != null) { sun.color = color; sun.intensity = intensity; }
            if (homeLight != null) homeLight.SetActive(phase == GamePhaseId.Home);
        }
    }
}
