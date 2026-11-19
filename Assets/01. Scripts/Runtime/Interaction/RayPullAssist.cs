using CardShop.Cleanup;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>
    /// 레이로 멀리서 쥔 청소 물건(쓰레기 · 흐트러진 팩 · 대걸레 = HeldItem)을 왼손 트리거를 꾹 누르는 동안 쥔 손 쪽으로 천천히 당깁니다 (FIX D1).
    /// 스틱 밀고 당기기(XRI 조작 입력)는 켜면 쥐는 동안 이동 · 회전이 꺼져서 쓰지 않음(DEC-017 유지, 11-19 사용자 결정).
    /// 손으로 순간 이동시키지 않는 이유: 결국 쓰레기통까지 걸어가야 해서 멀미 나는 사람이 생김 → 제자리에서 거리만 줄임.
    /// 카드 · 카드 팩 · 스캐너는 트리거에 자기 기능이 있어 HeldItem이 아니므로 대상이 아닙니다. 씬이 열리면 스스로 생깁니다.
    /// </summary>
    public sealed class RayPullAssist : MonoBehaviour
    {
        const float Speed = 1.5f;     // m/s
        const float StopAhead = 0.15f; // 손 앞 이 거리에서 멈춤 (m)

        NearFarInteractor[] hands;
        XRBaseInputInteractor leftHand;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            XROrigin rig = FindAnyObjectByType<XROrigin>();
            if (rig != null && rig.GetComponent<RayPullAssist>() == null) rig.gameObject.AddComponent<RayPullAssist>();
        }

        void Start()
        {
            hands = GetComponentsInChildren<NearFarInteractor>(true);
            foreach (NearFarInteractor h in hands)
                if (h.handedness == InteractorHandedness.Left) leftHand = h;
            if (leftHand == null) enabled = false;
        }

        void Update()
        {
            if (!leftHand.activateInput.ReadIsPerformed()) return;
            foreach (NearFarInteractor hand in hands)
            {
                if (hand == null || !hand.hasSelection || hand.selectionRegion.Value != NearFarInteractor.Region.Far) continue;
                if (!(hand.firstInteractableSelected is Component c) || c.GetComponentInParent<HeldItem>() == null) continue;
                IInteractionAttachController attach = hand.interactionAttachController;
                if (attach == null || attach.transformToFollow == null) continue;
                Transform follow = attach.transformToFollow;
                Vector3 now = attach.GetOrCreateAnchorTransform().position;
                Vector3 stop = follow.position + follow.forward * StopAhead;
                attach.MoveTo(Vector3.MoveTowards(now, stop, Speed * Time.deltaTime));
            }
        }
    }
}
