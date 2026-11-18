using CardShop.Core;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 페이즈에 따라 집 구역과 가게 바깥 구역을 켜고 끕니다 (2026-11-18 기기 테스트 피드백 M1: 밖에서 내 방이 보여 이질감).
    /// - 집(Home) 페이즈: 집 실내 켬, 바깥 거리 · 거리 생동감(차 · 행인) 끔
    /// - 그 밖의 페이즈: 집 실내 끔, 바깥 거리 켬
    /// 꺼진 쪽은 그리지 않으므로 Quest 그리기 비용도 줄어듭니다.
    ///
    /// 끄는 대상은 스크립트가 거의 없는 장식 묶음(인테리어 · 거리 프리팹)만 넣습니다. 바인더 · 커뮤니티처럼
    /// 다른 페이즈에도 이벤트를 받아야 하는 오브젝트는 넣지 않습니다.
    /// 페이즈 전환은 DayTransition이 화면을 가린 뒤 바꾸고 같은 프레임에 플레이어를 옮기므로, 켜고 끄는 순간은 보이지 않고
    /// 집 바닥 콜라이더도 플레이어가 도착하기 전에 켜집니다.
    /// </summary>
    public sealed class ZoneVisibility : PhaseListener
    {
        [Tooltip("집 페이즈에만 켜는 것 (집 실내 프리팹)")]
        [SerializeField] GameObject[] homeOnly;
        [Tooltip("집 페이즈가 아닐 때만 켜는 것 (바깥 거리 · 건물 · 차 · 행인)")]
        [SerializeField] GameObject[] outsideOnly;

        public void Configure(GameObject[] home, GameObject[] outside)
        {
            homeOnly = home;
            outsideOnly = outside;
        }

        protected override void Refresh()
        {
            bool home = CurrentPhase == GamePhaseId.Home;
            SetAll(homeOnly, home);
            SetAll(outsideOnly, !home);
        }

        static void SetAll(GameObject[] list, bool active)
        {
            if (list == null) return;
            foreach (GameObject go in list)
                if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
