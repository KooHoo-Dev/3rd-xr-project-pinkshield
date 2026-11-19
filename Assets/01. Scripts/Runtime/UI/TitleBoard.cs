using CardShop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 타이틀 판 (타이틀연출_손님_설계 0-2): 가게 앞 인도에 선 플레이어 앞에 게임 이름 · 크레딧 · [영업 시작] · [눈높이 맞추기] · [크레딧](CreditsButton).
    /// 세이브가 없으므로(설계서 "하지 않는 것") 이어하기는 없습니다. 글자(이름 · 크레딧)는 빌더가 고정으로 넣습니다.
    /// </summary>
    public sealed class TitleBoard : PhaseBoard
    {
        [SerializeField] Button recalibrateButton;
        [Tooltip("비우면 누를 때 씬에서 찾음 (플레이어 리그에 붙어 있음)")]
        [SerializeField] CardShop.Interaction.PlayerHeightCalibrator heightCalibrator;

        protected override GamePhaseId Phase => GamePhaseId.Title;

        /// <summary>버튼 모양을 복제할 원본 (튜토리얼 켬/끔 버튼)</summary>
        public Button RecalibrateButton => recalibrateButton;

        // [크레딧] 버튼은 씬을 다시 빌드하지 않도록 실행 중에 붙임 (음악 출처 표기)
        void Awake()
        {
            gameObject.AddComponent<AudioVolumeButtons>().Configure(this); // 크레딧 판보다 먼저 붙여 크레딧이 덮게 (11-20 H3)
            gameObject.AddComponent<CreditsButton>().Configure(this);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (recalibrateButton != null) recalibrateButton.onClick.AddListener(OnRecalibrate);
        }

        protected override void OnDisable()
        {
            if (recalibrateButton != null) recalibrateButton.onClick.RemoveListener(OnRecalibrate);
            base.OnDisable();
        }

        protected override void Redraw() { } // 고정 글자

        void OnRecalibrate()
        {
            if (heightCalibrator == null) heightCalibrator = FindAnyObjectByType<CardShop.Interaction.PlayerHeightCalibrator>();
            if (heightCalibrator == null) return;
            heightCalibrator.Calibrate();
            Context?.Events.RaiseNotice($"눈높이를 {heightCalibrator.TargetEyeHeight:0.0#}m로 맞췄어요.");
        }
    }
}
