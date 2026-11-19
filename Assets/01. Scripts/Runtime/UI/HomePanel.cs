using CardShop.Core;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 집(Home)에서만 보이는 패널(판매 · 커뮤니티)의 공통 부분: 집에 들어오면 켜고 다시 그림, 나가면 끔.
    /// 보유 카드가 바뀌면 한 프레임에 한 번만 다시 그립니다(보이는 동안만). 새 게임이면 새 Collection을 다시 구독합니다.
    /// </summary>
    public abstract class HomePanel : PhaseListener
    {
        [Tooltip("패널 내용을 담은 오브젝트. 집이 아닐 때 꺼집니다.")]
        [SerializeField] protected GameObject content;

        Collection collection;
        UpgradeState upgrades;
        bool dirty;

        protected bool IsVisible => content != null && content.activeSelf;

        protected override void OnEnable()
        {
            SetVisible(false);
            base.OnEnable();
        }

        protected override void Bind(GameSession session)
        {
            if (collection != null) collection.Changed -= OnCollectionChanged;
            if (upgrades != null) upgrades.Changed -= OnUpgradesChanged;
            collection = session?.Collection;
            upgrades = session?.Upgrades;
            if (collection != null) collection.Changed += OnCollectionChanged;
            if (upgrades != null) upgrades.Changed += OnUpgradesChanged; // 택배 상자를 열면 칸 수 · 예보가 바뀜
            dirty = true;
        }

        void OnCollectionChanged(string _) => dirty = true;
        void OnUpgradesChanged() => dirty = true;

        /// <summary>다음 LateUpdate에 다시 그림 (여러 번 불려도 한 번만).</summary>
        protected void MarkDirty() => dirty = true;

        void LateUpdate()
        {
            if (dirty && IsVisible) Refresh();
        }

        protected override void OnPhaseChanged(GamePhaseId phase)
        {
            bool home = phase == GamePhaseId.Home;
            SetVisible(home);
            if (home) Refresh();
        }

        protected override void OnDayStarted(int day) => dirty = true;

        protected sealed override void Refresh()
        {
            if (!IsVisible) { dirty = true; return; }
            dirty = false;
            Redraw();
        }

        void SetVisible(bool visible)
        {
            if (content != null) content.SetActive(visible);
        }

        /// <summary>패널 글자 · 버튼을 지금 상태로 채움 (보일 때만 불림).</summary>
        protected abstract void Redraw();
    }
}
