using System.Collections.Generic;
using CardShop.Core;
using TMPro;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 계산대 이름표("스캐너 (잡고 트리거)" · "봉투 (찍은 물건을 넣기)")는 영업 중에만 보이게 합니다 (11-20 F3).
    /// 퇴근 후 카드를 뜯을 때 팩 결과 글자와 겹쳤음. 이름표는 씬 빌더(CustomerFloorBuilder.Label)가 만든 것이라 씬을 다시 빌드하지 않도록
    /// 스캐너 이름표는 PlacementKey("Counter/ScannerLabel"), 봉투 이름표는 PaperBag 밑의 글자로 찾습니다. 씬이 열리면 스스로 생깁니다.
    /// </summary>
    public sealed class CounterLabels : PhaseListener
    {
        const string ScannerKey = "Counter/ScannerLabel";
        readonly List<GameObject> labels = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<CheckoutCounter>() == null || FindAnyObjectByType<CounterLabels>() != null) return;
            new GameObject("CounterLabels").AddComponent<CounterLabels>();
        }

        void Awake()
        {
            foreach (PlacementKey k in FindObjectsByType<PlacementKey>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (k.key == ScannerKey) labels.Add(k.gameObject);
            foreach (PaperBag bag in FindObjectsByType<PaperBag>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (TMP_Text t in bag.GetComponentsInChildren<TMP_Text>(true))
                    if (t.transform.parent == bag.transform) labels.Add(t.gameObject); // 봉투 바로 밑 이름표만 (담긴 물건 가격표는 아님)
        }

        protected override void Refresh()
        {
            bool show = CurrentPhase == GamePhaseId.Business || CurrentPhase == GamePhaseId.None;
            foreach (GameObject g in labels)
                if (g != null && g.activeSelf != show) g.SetActive(show);
        }
    }
}
