using System.Collections;
using System.Collections.Generic;
using CardShop.Core;
using CardShop.Presentation;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

namespace CardShop.Interaction
{
    /// <summary>
    /// 플레이어가 걸어 다녀도 되는 곳(ShopLayout.InsideWalkArea: 가게 안 · 가게 앞 인도 · 집) 밖으로 나가지 못하게 막습니다.
    /// 머리가 경계를 넘으면 ① 리그를 마지막으로 안전했던 자리로 되돌리고 ② 잠시 이동 입력(이동 · 회전 · 텔레포트)을 끄고
    /// ③ 혼잣말을 띄웁니다. 도로에는 바닥 콜라이더가 없어 예전에는 그대로 떨어졌음 (BUG-009).
    ///
    /// 실제로 걸어서(룸스케일) 나가는 경우도 머리 위치로 판정하므로 똑같이 막힙니다.
    /// 잡기 · 버튼 입력은 끄지 않습니다: 들고 있던 물건이 떨어지면 안 되기 때문입니다.
    /// </summary>
    public sealed class PlayAreaGuard : MonoBehaviour
    {
        [SerializeField] XROrigin origin;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] string message = "지금 알바 째면 카드팩은 못 까..";
        [Tooltip("경계에 닿은 뒤 이동 입력을 끄는 시간(초)")]
        [SerializeField] float lockSeconds = 2f;
        [Tooltip("리그가 이 높이 아래로 떨어지면 마지막 안전한 자리로 되돌림 (보험)")]
        [SerializeField] float fallLimitY = -2f;

        WorldToast toast;
        Vector3 lastSafeHead;
        bool hasSafe;
        Coroutine lockRoutine;
        readonly List<LocomotionProvider> disabled = new List<LocomotionProvider>();

        public void Configure(XROrigin xrOrigin, TMP_FontAsset fontAsset)
        {
            origin = xrOrigin;
            font = fontAsset;
        }

        void Awake() => toast = WorldToast.Create(transform, font);

        void LateUpdate()
        {
            if (origin == null || origin.Camera == null) return;
            Vector3 head = origin.Camera.transform.position;

            if (ShopLayout.InsideWalkArea(head) && origin.transform.position.y > fallLimitY)
            {
                lastSafeHead = head;
                hasSafe = true;
                return;
            }
            if (!hasSafe) return;

            Vector3 push = lastSafeHead - head;
            push.y = 0f;
            Vector3 rigPos = origin.transform.position + push;
            if (rigPos.y < fallLimitY) rigPos.y = 0f;
            Teleport(rigPos);

            if (lockRoutine == null) lockRoutine = StartCoroutine(LockMovement());
        }

        // CharacterController가 붙어 있으면 transform을 직접 바꿔도 다음 Move에서 원래 자리로 돌아가므로 잠깐 끄고 옮김
        void Teleport(Vector3 rigPos)
        {
            var cc = origin.GetComponent<CharacterController>();
            bool ccOn = cc != null && cc.enabled;
            if (ccOn) cc.enabled = false;
            origin.transform.position = rigPos;
            if (ccOn) cc.enabled = true;
        }

        IEnumerator LockMovement()
        {
            disabled.Clear();
            foreach (var provider in origin.GetComponentsInChildren<LocomotionProvider>())
            {
                if (provider is GravityProvider || !provider.enabled) continue; // 중력은 그대로 (발이 땅에 붙어 있게)
                provider.enabled = false;
                disabled.Add(provider);
            }
            ShowMessage();

            yield return new WaitForSeconds(lockSeconds);

            foreach (var provider in disabled)
                if (provider != null) provider.enabled = true;
            disabled.Clear();
            lockRoutine = null;
        }

        void OnDisable()
        {
            if (lockRoutine != null) StopCoroutine(lockRoutine);
            lockRoutine = null;
            foreach (var provider in disabled)
                if (provider != null) provider.enabled = true;
            disabled.Clear();
        }

        void ShowMessage()
        {
            if (toast == null) return;
            toast.Show(message, HeadPose.InFront(origin.Camera.transform, 1.0f, 0.1f).position);
        }
    }
}
