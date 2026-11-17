using CardShop.Presentation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>
    /// 바인더 표지의 소켓 1개 (설계서 7-3: 50칸에 소켓을 두지 않음).
    /// 받을 대상은 Interaction Layer(Card)로 제한하고, 혹시 층 설정이 빠져도 카드만 받도록 한 번 더 확인합니다.
    /// 꽂힌 카드를 칸으로 날려 보내는 일은 BinderView가 selectEntered를 듣고 처리합니다.
    /// </summary>
    public sealed class BinderCoverSocket : XRSocketInteractor
    {
        public override bool CanHover(IXRHoverInteractable interactable) =>
            base.CanHover(interactable) && IsCard(interactable);

        public override bool CanSelect(IXRSelectInteractable interactable) =>
            base.CanSelect(interactable) && IsCard(interactable);

        static bool IsCard(IXRInteractable interactable) =>
            interactable != null && interactable.transform.GetComponent<CardRevealView>() != null;
    }
}
