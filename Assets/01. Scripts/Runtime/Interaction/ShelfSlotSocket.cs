using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>
    /// 선반 칸. 팩만 받고, 놓는 순간의 앞/뒤 방향을 판정해 앞면 자세 / 뒷면 자세 중 하나로 스냅합니다.
    /// 그래서 플레이어가 뒤집어 둔 방향이 유지됩니다 (설계서 7-6).
    /// </summary>
    public sealed class ShelfSlotSocket : XRSocketInteractor
    {
        [SerializeField] Transform faceUpAttach;
        [SerializeField] Transform faceDownAttach;

        bool faceDown;

        public Transform FaceUpAttach => faceUpAttach;
        public Transform FaceDownAttach => faceDownAttach;
        public Transform AttachFor(bool isFaceDown) => isFaceDown ? faceDownAttach : faceUpAttach;

        public void Configure(Transform up, Transform down)
        {
            faceUpAttach = up;
            faceDownAttach = down;
        }

        public override bool CanHover(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRHoverInteractable interactable) =>
            base.CanHover(interactable) && IsPack(interactable);

        public override bool CanSelect(IXRSelectInteractable interactable) =>
            base.CanSelect(interactable) && IsPack(interactable);

        public override Transform GetAttachTransform(IXRInteractable interactable)
        {
            // 아직 잡고 있지 않을 때만 방향을 다시 판정 (꽂힌 뒤에는 고정)
            if (!hasSelection && interactable != null)
            {
                var pack = interactable.transform.GetComponent<PackView>();
                if (pack != null) faceDown = pack.IsFaceDown;
            }
            Transform t = AttachFor(faceDown);
            return t != null ? t : base.GetAttachTransform(interactable);
        }

        static bool IsPack(IXRInteractable interactable) =>
            interactable != null && interactable.transform.GetComponent<PackView>() != null;
    }
}
