using CardShop.Cleanup;
using CardShop.Logic;
using CardShop.Presentation;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace CardShop.Interaction
{
    /// <summary>
    /// 손 물건을 쥘 때 재질 소리 (11-21 피드백 S3: 팩 바스락 · 상자 툭 · 쓰레기 캔 · 카드 사락 · 그 밖 딸깍).
    /// InteractableOutline처럼 XRInteractionManager에 등록되는 물건에 스스로 붙습니다 (나중에 생기는 팩 · 쓰레기 포함).
    /// 떨어질 때 소리도 여기서 연결합니다: 청소 물건(HeldItem, 물리 없이 내려앉음)은 내려앉을 때,
    /// 물리로 움직이는 물건은 ImpactSound가 부딪힐 때. 종류 판단은 GrabSoundRules(순수, 테스트).
    /// 공개 카드(CardRevealView)는 스스로 CardPick을 내므로 잡기 소리는 건너뜁니다.
    /// </summary>
    public sealed class GrabSound : MonoBehaviour
    {
        XRBaseInteractable interactable;
        HeldItem held;
        bool selfSounding;

        /// <summary>이 물건의 소리 종류</summary>
        public GrabKind Kind { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            foreach (XRInteractionManager m in FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None))
            {
                m.interactableRegistered -= OnRegistered;
                m.interactableRegistered += OnRegistered;
            }
            foreach (XRBaseInteractable i in FindObjectsByType<XRBaseInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Attach(i);
        }

        static void OnRegistered(InteractableRegisteredEventArgs args)
        {
            if (args.interactableObject is XRBaseInteractable i) Attach(i);
        }

        // 쥘 수 있는 물건만 (버튼 · 순간이동 바닥 · 손잡이 없는 UI는 뺌)
        static void Attach(XRBaseInteractable i)
        {
            if (i == null || i is BaseTeleportationInteractable || i is not XRGrabInteractable || i.GetComponent<GrabSound>() != null) return;
            i.gameObject.AddComponent<GrabSound>();
        }

        void Start()
        {
            interactable = GetComponent<XRBaseInteractable>();
            if (interactable == null) { Destroy(this); return; }
            Kind = GrabSoundRules.Classify(
                GetComponent<TrashSpot>() != null,
                GetComponent<RackPack>() != null || GetComponent<PackView>() != null,
                GetComponent<DeliveryBox>() != null || GetComponentInParent<DeliveryBox>() != null,
                name);
            selfSounding = GetComponent<CardRevealView>() != null;
            interactable.selectEntered.AddListener(OnGrab);

            held = GetComponent<HeldItem>();
            if (held != null) held.Landed += OnLanded;
            else if (TryGetComponent(out Rigidbody body) && !body.isKinematic && GetComponent<ImpactSound>() == null)
                gameObject.AddComponent<ImpactSound>().Init(this);
        }

        void OnDestroy()
        {
            if (interactable != null) interactable.selectEntered.RemoveListener(OnGrab);
            if (held != null) held.Landed -= OnLanded;
        }

        void OnGrab(SelectEnterEventArgs _)
        {
            if (!selfSounding) Sfx.Play(GrabSoundRules.PickSound(Kind), transform.position);
        }

        void OnLanded(float fallHeight)
        {
            if (fallHeight < GrabSoundRules.MinDropHeight) return;
            float speed = Mathf.Sqrt(2f * 9.81f * fallHeight); // 그 높이에서 떨어진 속도
            Sfx.PlayScaled(GrabSoundRules.FallSound(Kind), transform.position, GrabSoundRules.ImpactVolume(speed));
        }
    }
}
