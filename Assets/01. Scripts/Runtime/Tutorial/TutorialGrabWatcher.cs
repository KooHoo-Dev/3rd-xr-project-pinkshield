using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 플레이어 손(컨트롤러 인터랙터)이 지금 잡고 있는 물건을 모읍니다. 소켓은 손이 아니므로 뺍니다.
    /// 튜토리얼이 "선물 팩 · 빗자루 · 스캐너를 잡았는가"와 "엉뚱한 물건을 잡았는가"(막지 않고 알려 주기)를 판단할 때 씁니다.
    /// 인터랙터 목록은 단계가 시작될 때만 다시 찾습니다(매 프레임 Find 금지).
    /// </summary>
    public sealed class TutorialGrabWatcher
    {
        readonly List<XRBaseInputInteractor> hands = new List<XRBaseInputInteractor>();

        public void Refresh()
        {
            hands.Clear();
            foreach (XRBaseInputInteractor i in Object.FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None))
                hands.Add(i);
        }

        /// <summary>잡고 있는 물건들의 Transform을 results에 담습니다 (먼저 비움).</summary>
        public void Collect(List<Transform> results)
        {
            results.Clear();
            for (int h = 0; h < hands.Count; h++)
            {
                XRBaseInputInteractor hand = hands[h];
                if (hand == null || !hand.hasSelection) continue;
                List<IXRSelectInteractable> selected = hand.interactablesSelected;
                for (int k = 0; k < selected.Count; k++)
                    if (selected[k] is Component c && c != null) results.Add(c.transform);
            }
        }

        /// <summary>held가 target과 같은 물건이거나 그 자식 · 부모인가</summary>
        public static bool Related(Transform held, Transform target) =>
            held != null && target != null && (held == target || held.IsChildOf(target) || target.IsChildOf(held));
    }
}
