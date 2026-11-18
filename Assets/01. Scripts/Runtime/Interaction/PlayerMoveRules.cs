using System.Collections.Generic;
using CardShop.Core;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace CardShop.Interaction
{
    /// <summary>
    /// 플레이어 이동 규칙 두 가지 (11-19 플레이 피드백 J).
    /// ① 오른쪽 스틱을 아래로 당기면 180도 뒤로 도는 기능(XRI turn around)을 끕니다 — 갑자기 돌아 어지러움.
    /// ② 일시정지 중에는 이동 · 회전 · 텔레포트를 막습니다 — 메뉴를 연 채 움직여 생기는 버그를 원천 봉쇄.
    ///    잡기 · UI 레이는 그대로입니다(메뉴 버튼을 눌러야 하고, 들고 있던 물건이 떨어지면 안 되므로).
    ///    찍기 · 담기 · 청소 같은 게임 처리는 각 클래스가 이미 일시정지 중에 받지 않습니다.
    ///
    /// 씬을 다시 빌드하지 않아도 되게, 씬이 열리면 XR Origin에 스스로 붙습니다.
    /// PlayAreaGuard(경계에서 2초 잠금)와 같은 이동 제공자를 켜고 끄므로, 일시정지 중에는 매 프레임 꺼진 상태를 유지합니다.
    /// </summary>
    public sealed class PlayerMoveRules : MonoBehaviour
    {
        XROrigin origin;
        GameContext context;
        bool paused;
        readonly List<LocomotionProvider> locked = new List<LocomotionProvider>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            XROrigin rig = FindAnyObjectByType<XROrigin>();
            if (rig != null && rig.GetComponent<PlayerMoveRules>() == null) rig.gameObject.AddComponent<PlayerMoveRules>();
        }

        void Awake()
        {
            origin = GetComponent<XROrigin>();
            DisableTurnAround();
        }

        void OnEnable() => Subscribe();
        void Start() => Subscribe();

        void OnDisable()
        {
            if (context != null) context.Events.PausedChanged -= OnPausedChanged;
            context = null;
            Unlock();
        }

        void Subscribe()
        {
            if (context != null || GameContext.Instance == null) return;
            context = GameContext.Instance;
            context.Events.PausedChanged += OnPausedChanged;
        }

        void DisableTurnAround()
        {
            foreach (SnapTurnProvider p in GetComponentsInChildren<SnapTurnProvider>(true)) p.enableTurnAround = false;
            foreach (ContinuousTurnProvider p in GetComponentsInChildren<ContinuousTurnProvider>(true)) p.enableTurnAround = false;
        }

        void OnPausedChanged(bool value)
        {
            paused = value;
            if (!paused) Unlock();
        }

        // 일시정지 중: 켜져 있는 이동 제공자를 끄고 기억 (PlayAreaGuard가 잠금을 풀며 다시 켜도 다음 프레임에 다시 끔)
        void LateUpdate()
        {
            if (!paused || origin == null) return;
            foreach (LocomotionProvider p in origin.GetComponentsInChildren<LocomotionProvider>())
            {
                if (p is GravityProvider || !p.enabled) continue; // 중력은 그대로 (발이 땅에 붙어 있게)
                p.enabled = false;
                if (!locked.Contains(p)) locked.Add(p);
            }
        }

        void Unlock()
        {
            foreach (LocomotionProvider p in locked)
                if (p != null) p.enabled = true;
            locked.Clear();
        }
    }
}
