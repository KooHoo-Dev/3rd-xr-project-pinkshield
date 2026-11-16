using System;
using System.Collections;
using CardShop.Cards;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Presentation
{
    /// <summary>
    /// 공개 연출용 카드 상태. 앞/뒤 뒤집기는 물리와 무관하게 "Visual" 자식만 회전합니다(손에 든 채로도 뒤집힘).
    /// </summary>
    [RequireComponent(typeof(CardView), typeof(XRGrabInteractable))]
    public sealed class CardRevealView : MonoBehaviour
    {
        const float FlipSeconds = 0.25f;

        CardView view;
        XRGrabInteractable grab;
        Rigidbody body;
        Transform visual;
        bool faceDown;

        public CardData Card => view.Data;
        public bool IsNew { get; private set; }
        public event Action<CardRevealView> Revealed;

        void Awake()
        {
            view = GetComponent<CardView>();
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            visual = transform.Find("Visual");
            if (visual == null) Debug.LogWarning("[CardRevealView] 카드 프리팹에 Visual 자식이 없습니다. Build Card Prefab 을 다시 실행하세요.", this);
            grab.selectEntered.AddListener(OnGrabbed);
        }

        public void Show(CardData card, bool isNew, Vector3 position, Quaternion rotation, bool startFaceDown)
        {
            gameObject.SetActive(true);
            view.Bind(card);
            IsNew = isNew;
            transform.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.isKinematic = true; // 놓아도 그 자리에 떠 있음 (잡았다 놓으면 XRI가 이 상태로 되돌림)
                body.useGravity = false;
            }
            faceDown = startFaceDown;
            if (visual != null) visual.localRotation = faceDown ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            if (!faceDown) Revealed?.Invoke(this);
        }

        public void Hide()
        {
            if (grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        void OnGrabbed(SelectEnterEventArgs _)
        {
            if (!faceDown) return;
            faceDown = false;
            if (visual != null) StartCoroutine(Flip());
            Revealed?.Invoke(this);
        }

        IEnumerator Flip()
        {
            Quaternion from = visual.localRotation, to = Quaternion.identity;
            for (float t = 0f; t < 1f; t += Time.deltaTime / FlipSeconds)
            {
                visual.localRotation = Quaternion.Slerp(from, to, t);
                yield return null;
            }
            visual.localRotation = to;
        }
    }
}
