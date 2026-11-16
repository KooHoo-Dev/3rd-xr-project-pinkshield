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
            grab.throwOnDetach = false; // 공개용 카드는 키네마틱이라 던질 수 없음 (XRI 경고 방지)
            if (body != null)
            {
                // 보간이 켜져 있으면 스크립트로 옮긴 Transform을 옛 물리 위치로 덮어써서
                // 카드가 이전 자리(카운터)로 튀어 돌아갑니다 (BUG-003). 공개용 카드는 스크립트로만 움직이므로 끕니다.
                body.interpolation = RigidbodyInterpolation.None;
            }
        }

        /// <param name="announce">앞면으로 놓을 때 바로 공개 이벤트를 낼지. 묶음 뒤쪽 카드는 맨 앞에 올 때 <see cref="Announce"/>로 냅니다.</param>
        public void Show(CardData card, bool isNew, Vector3 position, Quaternion rotation, bool startFaceDown, bool announce = true)
        {
            gameObject.SetActive(true);
            view.Bind(card);
            IsNew = isNew;
            if (body != null)
            {
                body.isKinematic = true; // 놓아도 그 자리에 떠 있음 (잡았다 놓으면 XRI가 이 상태로 되돌림)
                body.useGravity = false;
            }
            Teleport(position, rotation);
            faceDown = startFaceDown;
            if (visual != null) visual.localRotation = faceDown ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            grab.enabled = true;
            if (!faceDown && announce) Revealed?.Invoke(this);
        }

        public void Announce() => Revealed?.Invoke(this);

        /// <summary>묶음에 있는 동안은 잡히지 않게(넘기기 순서가 꼬이지 않도록).</summary>
        public void SetGrabbable(bool on) => grab.enabled = on;

        /// <summary>살짝 위로 포물선을 그리며 날아가 놓이고, 도착하면 잡을 수 있게 됩니다.</summary>
        public void FlyTo(Vector3 position, Quaternion rotation, float seconds)
        {
            SetGrabbable(false);
            StartCoroutine(Fly(position, rotation, seconds));
        }

        IEnumerator Fly(Vector3 toPos, Quaternion toRot, float seconds)
        {
            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                float e = Mathf.SmoothStep(0f, 1f, t);
                Vector3 arc = Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.06f);
                Teleport(Vector3.Lerp(fromPos, toPos, e) + arc, Quaternion.Slerp(fromRot, toRot, e));
                yield return null;
            }
            Teleport(toPos, toRot);
            SetGrabbable(true);
        }

        // Transform과 Rigidbody를 함께 옮겨, 다음 물리 스텝에서 옛 위치로 되돌아가지 않게 합니다.
        void Teleport(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
            }
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
