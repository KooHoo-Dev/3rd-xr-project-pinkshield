using System.Collections.Generic;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 집 컴퓨터 모니터의 쪽 하나 (집_책상_하나로_설계 4장). HomeComputer가 넘길 때마다 보이는 정도 · 미끄러진 거리를 넘겨 줍니다.
    /// </summary>
    public interface IComputerPage
    {
        /// <param name="amount">0 = 숨김, 1 = 다 보임 (넘기는 동안 그 사이)</param>
        /// <param name="offset">제자리에서 옆으로 미끄러진 거리 (부모 로컬)</param>
        /// <param name="interactive">지금 쪽이고 집에 있을 때만 true — 레이를 받음</param>
        void Show(float amount, Vector3 offset, bool interactive);
    }

    /// <summary>캔버스 쪽 (마켓 · 판매 · 진열): 캔버스 여러 장을 CanvasGroup 투명도로 함께 숨김.</summary>
    public sealed class CanvasPage : IComputerPage
    {
        readonly List<RectTransform> roots = new List<RectTransform>();
        readonly List<Vector3> basePos = new List<Vector3>();
        readonly List<CanvasGroup> groups = new List<CanvasGroup>();

        public CanvasPage(params RectTransform[] canvases)
        {
            foreach (RectTransform rt in canvases) Add(rt);
        }

        /// <summary>캔버스를 지금 자리 기준으로 쪽에 넣음 (자리를 다 잡은 뒤 부름)</summary>
        public void Add(RectTransform rt)
        {
            if (rt == null) return;
            roots.Add(rt);
            basePos.Add(rt.localPosition);
            groups.Add(rt.TryGetComponent(out CanvasGroup g) ? g : rt.gameObject.AddComponent<CanvasGroup>());
        }

        public void Show(float amount, Vector3 offset, bool interactive)
        {
            for (int i = 0; i < roots.Count; i++)
            {
                roots[i].localPosition = basePos[i] + offset;
                groups[i].alpha = amount;
                groups[i].blocksRaycasts = groups[i].interactable = interactive;
            }
        }
    }
}
