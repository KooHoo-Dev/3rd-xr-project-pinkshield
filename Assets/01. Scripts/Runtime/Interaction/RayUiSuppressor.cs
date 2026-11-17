using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>
    /// 한 손의 레이 UI 상호작용만 잠시 끄고(잡기 · 근접은 그대로) 나중에 원래대로 되돌립니다.
    /// 오른손 트리거가 카드 넘기기와 Ray UI 클릭을 동시에 하지 않게 할 때 씁니다 (CardRevealer).
    /// </summary>
    public sealed class RayUiSuppressor
    {
        XRBaseInputInteractor hand;
        bool wasEnabled;

        /// <summary>이미 끈 손이 있거나 hand가 레이 UI를 쓰지 않으면 아무것도 하지 않습니다.</summary>
        public void Suppress(XRBaseInputInteractor target)
        {
            if (hand != null || target == null) return;
            if (target is NearFarInteractor nearFar)
            {
                wasEnabled = nearFar.enableUIInteraction;
                nearFar.enableUIInteraction = false;
            }
            else if (target is XRRayInteractor ray)
            {
                wasEnabled = ray.enableUIInteraction;
                ray.enableUIInteraction = false;
            }
            else return;
            hand = target;
        }

        public void Restore()
        {
            if (hand == null) { hand = null; return; } // 파괴된 경우도 정리 (Unity null)
            if (hand is NearFarInteractor nearFar) nearFar.enableUIInteraction = wasEnabled;
            else if (hand is XRRayInteractor ray) ray.enableUIInteraction = wasEnabled;
            hand = null;
        }
    }
}
