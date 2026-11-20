using System;
using System.Collections;
using CardShop.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Presentation
{
    /// <summary>
    /// 공개 연출용 카드 상태. 늘 앞면으로 놓입니다(뒤집기 연출은 DEC-004 수정으로 뺌). 흔들림은 "Visual" 자식만 움직여 물리와 무관합니다.
    /// </summary>
    [RequireComponent(typeof(CardView), typeof(XRGrabInteractable))]
    public sealed class CardRevealView : MonoBehaviour
    {
        // NEW 배지: TMP 3D 글자 크기는 1당 약 10cm라 0.2면 글자 높이 2cm(카드 폭의 거의 전부)였음.
        // 0.35 → 0.2 → 0.045 (BUG-004: 현재의 20%)
        const float NewBadgeFontSize = 0.045f;

        CardView view;
        XRGrabInteractable grab;
        Rigidbody body;
        Transform visual;
        TextMeshPro newBadge;
        Coroutine flyRoutine;

        public CardData Card => view.Data;
        public bool IsNew { get; private set; }

        /// <summary>지금 손에 잡혀 있는지.</summary>
        public bool IsHeld => grab != null && grab.isSelected;
        /// <summary>손에 잡았을 때 / 놓았을 때 (CardRevealer의 더미 정보 표시용).</summary>
        public event Action<CardRevealView> GrabStarted;
        public event Action<CardRevealView> GrabEnded;

        void Awake()
        {
            view = GetComponent<CardView>();
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            visual = transform.Find("Visual");
            if (visual == null) Debug.LogWarning("[CardRevealView] 카드 프리팹에 Visual 자식이 없습니다. Build Card Prefab 을 다시 실행하세요.", this);
            grab.selectEntered.AddListener(_ => { PlayPickSound(); GrabStarted?.Invoke(this); });
            grab.selectExited.AddListener(_ => GrabEnded?.Invoke(this));
            grab.throwOnDetach = false; // 공개용 카드는 키네마틱이라 던질 수 없음 (XRI 경고 방지)
            if (body != null)
            {
                // 보간이 켜져 있으면 스크립트로 옮긴 Transform을 옛 물리 위치로 덮어써서
                // 카드가 이전 자리(카운터)로 튀어 돌아갑니다 (BUG-003). 공개용 카드는 스크립트로만 움직이므로 끕니다.
                body.interpolation = RigidbodyInterpolation.None;
            }
        }

        public void Show(CardData card, bool isNew, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            gameObject.SetActive(true);
            transform.localScale = Vector3.one * scale;
            view.Bind(card);
            IsNew = isNew;
            SetNewBadge(isNew);
            if (body != null)
            {
                body.isKinematic = true; // 놓아도 그 자리에 떠 있음 (잡았다 놓으면 XRI가 이 상태로 되돌림)
                body.useGravity = false;
            }
            Teleport(position, rotation);
            if (visual != null) visual.localPosition = Vector3.zero; // 흔들림 도중에 풀로 돌아간 경우
            if (visual != null) visual.localRotation = Quaternion.identity;
            grab.enabled = true;
        }

        /// <summary>카드를 부르르 떨게 함 (레전더리 연출. 카메라 셰이크 대신 — VR 멀미 방지). Visual만 움직여 물리와 무관.</summary>
        public void Shake(float amplitude, float seconds)
        {
            if (visual != null) StartCoroutine(ShakeRoutine(amplitude, Mathf.Max(0.05f, seconds)));
        }

        IEnumerator ShakeRoutine(float amplitude, float seconds)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                float fade = 1f - t;
                float x = Mathf.Sin(t * 95f) * amplitude * fade;
                float y = Mathf.Sin(t * 123f + 1.7f) * amplitude * 0.6f * fade;
                visual.localPosition = new Vector3(x, y, 0f);
                yield return null;
            }
            visual.localPosition = Vector3.zero;
        }

        // 처음 얻은 카드에 "NEW" 스티커 (개봉연출 설계 5장 #8). Visual 아래라 카드와 함께 뒤집힙니다.
        void SetNewBadge(bool on)
        {
            if (!on && newBadge == null) return;
            if (newBadge == null) newBadge = CreateNewBadge();
            if (newBadge != null) newBadge.gameObject.SetActive(on);
        }

        TextMeshPro CreateNewBadge()
        {
            Transform parent = visual != null ? visual : transform;
            var mesh = parent.GetComponentInChildren<MeshFilter>();
            if (mesh == null || mesh.sharedMesh == null) return null;
            Bounds b = mesh.sharedMesh.bounds; // 카드 크기 (Visual 기준 로컬)

            var go = new GameObject("NewBadge", typeof(RectTransform));
            go.transform.SetParent(mesh.transform, false);
            var text = go.AddComponent<TextMeshPro>();
            TMP_Text cardText = GetComponentInChildren<TMP_Text>(true);
            if (cardText != null && cardText != text) text.font = cardText.font;
            text.text = "<mark=#1F9D55E6><b> NEW </b></mark>";
            text.fontSize = NewBadgeFontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var badgeRenderer = text.GetComponent<MeshRenderer>();
            badgeRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0.016f, 0.006f);
            // BUG-004·006: 카드 앞면 위 3D 조각으로 띄우면 보는 각도·정렬에 따라 카드 뒤로 숨음.
            // 카드 글자(이름·등급)와 똑같은 z(앞면 바로 앞 0.3mm)에, 글자들과 겹치지 않는 그림 영역 오른쪽 위에 둡니다.
            rt.localPosition = new Vector3(b.min.x + b.size.x * 0.84f, b.min.y + b.size.y * 0.835f, b.min.z - 0.0003f);
            rt.localRotation = Quaternion.identity;
            return text;
        }

        // 카드를 쥐는 순간 "사락" (공개 카드 · 보관함 카드, F4). 양손으로 연달아 쥐어도 겹치지 않게 0.1초 간격
        const float PickGap = 0.1f;
        static float lastPick = -1f;

        void PlayPickSound()
        {
            if (Time.unscaledTime - lastPick < PickGap) return;
            lastPick = Time.unscaledTime;
            Sfx.Play(SoundId.CardPick, transform.position);
        }

        /// <summary>묶음에 있는 동안은 잡히지 않게(넘기기 순서가 꼬이지 않도록).</summary>
        public void SetGrabbable(bool on) => grab.enabled = on;

        /// <summary>
        /// 살짝 위로 포물선을 그리며 날아가 놓입니다. 이미 날아가는 중이면 그 비행을 멈추고 지금 자리에서 새로 출발합니다.
        /// </summary>
        /// <param name="arcHeight">포물선 꼭대기 높이(m). 0이면 곧게 이동</param>
        /// <param name="grabbableOnArrive">도착하면 잡을 수 있게 할지. 카드 더미는 순서가 꼬이지 않게 false</param>
        /// <param name="delay">출발 전 기다리는 시간(초)</param>
        public void FlyTo(Vector3 position, Quaternion rotation, float seconds, float toScale = 1f,
            float arcHeight = 0.06f, bool grabbableOnArrive = true, float delay = 0f)
        {
            SetGrabbable(false);
            if (flyRoutine != null) StopCoroutine(flyRoutine);
            flyRoutine = StartCoroutine(Fly(position, rotation, seconds, toScale, arcHeight, grabbableOnArrive, delay));
        }

        IEnumerator Fly(Vector3 toPos, Quaternion toRot, float seconds, float toScale, float arcHeight, bool grabbableOnArrive, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Vector3 fromScale = transform.localScale;
            Vector3 toScaleV = Vector3.one * toScale;
            Vector3 fromPos = transform.position;
            Quaternion fromRot = transform.rotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                float e = Mathf.SmoothStep(0f, 1f, t);
                Vector3 arc = Vector3.up * (Mathf.Sin(t * Mathf.PI) * arcHeight);
                Teleport(Vector3.Lerp(fromPos, toPos, e) + arc, Quaternion.Slerp(fromRot, toRot, e));
                transform.localScale = Vector3.Lerp(fromScale, toScaleV, e);
                yield return null;
            }
            transform.localScale = toScaleV;
            Teleport(toPos, toRot);
            flyRoutine = null;
            if (grabbableOnArrive) SetGrabbable(true);
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
            flyRoutine = null;
            gameObject.SetActive(false);
        }
    }
}
