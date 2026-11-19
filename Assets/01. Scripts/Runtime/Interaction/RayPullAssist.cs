using CardShop.Cleanup;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>
    /// 레이로 멀리서 쥔 물건을 트리거를 꾹 누르는 동안 쥔 손 쪽으로 천천히 당깁니다 (FIX D1, 11-20 C4 · C7).
    /// 청소 물건(쓰레기 · 흐트러진 팩 · 대걸레 = HeldItem)은 트리거에 다른 기능이 없어 **양손 트리거 어느 쪽이든** 당깁니다.
    /// 카드 · 카드 팩 · 스캐너처럼 트리거에 자기 기능이 있는 물건은 **반대 손 트리거**로만 당깁니다(쥔 손 트리거는 원래 기능).
    /// 스틱 밀고 당기기(XRI 조작 입력)는 켜면 쥐는 동안 이동 · 회전이 꺼져서 쓰지 않음(DEC-017 유지, 11-19 사용자 결정).
    /// 손으로 순간 이동시키지 않는 이유: 결국 쓰레기통까지 걸어가야 해서 멀미 나는 사람이 생김 → 제자리에서 거리만 줄임.
    /// 씬이 열리면 스스로 생깁니다.
    /// </summary>
    public sealed class RayPullAssist : MonoBehaviour
    {
        const float Speed = 1.5f;     // m/s
        const float StopAhead = 0.15f; // 손 앞 이 거리에서 멈춤 (m)

        NearFarInteractor[] hands;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            XROrigin rig = FindAnyObjectByType<XROrigin>();
            if (rig != null && rig.GetComponent<RayPullAssist>() == null) rig.gameObject.AddComponent<RayPullAssist>();
        }

        void Start()
        {
            hands = GetComponentsInChildren<NearFarInteractor>(true);
            if (hands.Length == 0) enabled = false;
        }

        void Update()
        {
            foreach (NearFarInteractor hand in hands)
            {
                if (hand == null || !hand.hasSelection || hand.selectionRegion.Value != NearFarInteractor.Region.Far) continue;
                if (!(hand.firstInteractableSelected is Component c)) continue;
                bool cleanup = c.GetComponentInParent<HeldItem>() != null;
                if (!(cleanup ? AnyTrigger() : OtherTrigger(hand))) continue;
                IInteractionAttachController attach = hand.interactionAttachController;
                if (attach == null || attach.transformToFollow == null) continue;
                Transform follow = attach.transformToFollow;
                Vector3 now = attach.GetOrCreateAnchorTransform().position;
                Vector3 stop = follow.position + follow.forward * StopAhead;
                attach.MoveTo(Vector3.MoveTowards(now, stop, Speed * Time.deltaTime));
            }
        }

        bool AnyTrigger()
        {
            foreach (NearFarInteractor h in hands)
                if (h != null && h.activateInput.ReadIsPerformed()) return true;
            return false;
        }

        // 쥔 손이 아닌 손의 트리거
        bool OtherTrigger(NearFarInteractor holding)
        {
            foreach (NearFarInteractor h in hands)
                if (h != null && h != holding && h.handedness != holding.handedness && h.activateInput.ReadIsPerformed()) return true;
            return false;
        }
    }
}
