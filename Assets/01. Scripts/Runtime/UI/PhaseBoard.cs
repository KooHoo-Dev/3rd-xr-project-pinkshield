using CardShop.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 한 페이즈에서만 보이는 월드 판(타이틀 · 정산 · 최종)의 공통 부분 (타이틀연출_손님_설계 0-3).
    /// 출시 빌드에는 디버그 "다음" 버튼이 없으므로, 이 판의 버튼이 그 페이즈를 넘기는 유일한 길입니다.
    /// 글자는 페이즈가 바뀐 다음 LateUpdate에 채웁니다: PhaseChanged가 페이즈의 Enter보다 먼저 불려
    /// 그 순간에는 정산 합계 · 최종 점수가 아직 계산 전이기 때문입니다.
    /// </summary>
    public abstract class PhaseBoard : PhaseListener
    {
        [Tooltip("판 전체. 해당 페이즈가 아니면 꺼집니다.")]
        [SerializeField] protected GameObject content;
        [SerializeField] protected TMP_Text heading;
        [SerializeField] protected TMP_Text body;
        [SerializeField] Button advanceButton;

        bool dirty;

        /// <summary>페이즈를 넘기는 버튼 (최종 판이 복제해 [계속하기]를 만듦, J4)</summary>
        protected Button AdvanceButton => advanceButton;

        /// <summary>이 판이 보이는 페이즈</summary>
        protected abstract GamePhaseId Phase { get; }

        protected bool IsVisible => content != null && content.activeSelf;

        protected override void OnEnable()
        {
            if (content != null) content.SetActive(false);
            if (advanceButton != null) advanceButton.onClick.AddListener(OnAdvance);
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            if (advanceButton != null) advanceButton.onClick.RemoveListener(OnAdvance);
            base.OnDisable();
        }

        protected override void Refresh()
        {
            bool show = CurrentPhase == Phase;
            if (content != null && content.activeSelf != show) content.SetActive(show);
            dirty = show;
        }

        void LateUpdate()
        {
            if (!dirty || !IsVisible || Context == null || Context.Session == null) return;
            dirty = false;
            Redraw();
        }

        // 페이드 중 · 다른 페이즈에서 눌린 클릭은 무시 (판이 꺼지기 직전 두 번 눌림 방지는 GameFlow.IsTransitioning)
        void OnAdvance()
        {
            if (Flow != null && Flow.CurrentPhase == Phase) Flow.RequestAdvance();
        }

        /// <summary>제목 · 본문을 지금 상태로 채움 (보일 때만, 페이즈가 바뀐 다음 프레임에 한 번)</summary>
        protected abstract void Redraw();
    }
}
