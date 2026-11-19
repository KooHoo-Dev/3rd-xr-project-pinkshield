using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Tutorial
{
    /// <summary>튜토리얼 중 엉뚱한 물건을 잡았을 때 (DEC-028: 기본은 잠그기)</summary>
    public enum OutsideGrabMode
    {
        Notify = 0, // 막지 않고 알려 주기
        Lock = 1,   // 대상 말고는 바로 놓게 함
    }

    /// <summary>
    /// 튜토리얼의 "다른 물건 잠그기" (DEC-028). 기다림 · 정지 단계 동안 대상과 그 짝(스캐너 · 계산대 물건)이 아닌 물건을 손이 잡으면
    /// 바로 놓게 하고 그 손을 진동시킨 뒤 <see cref="Denied"/>를 알립니다(자막 "지금은 이것부터!").
    /// 잡히지 않게 막는 방식(interactionLayers 비우기)은 플레이어가 왜 안 잡히는지 몰라서 쓰지 않습니다.
    /// 잠글 때 이미 손에 든 물건은 그대로 둡니다(놓을 수 없게 되는 일 방지). 손은 잡기 버튼을 새로 눌러야 다시 잡으므로(StateChange) 놓고 잡기가 반복되지 않습니다.
    /// </summary>
    public sealed class InteractionLock : MonoBehaviour
    {
        readonly List<Transform> keep = new List<Transform>();
        readonly List<XRBaseInputInteractor> hands = new List<XRBaseInputInteractor>();
        readonly HashSet<IXRSelectInteractable> heldAtLock = new HashSet<IXRSelectInteractable>();
        readonly List<IXRSelectInteractable> release = new List<IXRSelectInteractable>();

        public bool IsLocked { get; private set; }

        /// <summary>엉뚱한 물건을 놓게 했을 때</summary>
        public event Action Denied;

        /// <param name="keepList">놓게 하지 않을 대상들 (그 자식 · 부모 물건도 함께 남김)</param>
        public void Lock(IReadOnlyList<Transform> keepList)
        {
            Unlock();
            keep.AddRange(keepList);
            foreach (XRBaseInputInteractor hand in FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None))
            {
                hands.Add(hand);
                foreach (IXRSelectInteractable it in hand.interactablesSelected) heldAtLock.Add(it);
            }
            IsLocked = true;
        }

        public void Unlock()
        {
            IsLocked = false;
            keep.Clear();
            hands.Clear();
            heldAtLock.Clear();
        }

        void OnDisable() => Unlock();

        void LateUpdate()
        {
            if (!IsLocked) return;
            foreach (XRBaseInputInteractor hand in hands)
            {
                if (hand == null || !hand.hasSelection || hand.interactionManager == null) continue;
                release.Clear();
                foreach (IXRSelectInteractable it in hand.interactablesSelected)
                    if (it is Component c && c != null && !heldAtLock.Contains(it) && !Keeps(c.transform) && !TutorialGrabWatcher.IsHelper(c.transform))
                        release.Add(it);
                if (release.Count == 0) continue;
                foreach (IXRSelectInteractable it in release) hand.interactionManager.SelectExit(hand, it);
                hand.SendHapticImpulse(0.35f, 0.06f); // 짧고 약하게 (11-19 2차)
                Denied?.Invoke();
            }
        }

        bool Keeps(Transform t)
        {
            for (int i = 0; i < keep.Count; i++)
                if (TutorialGrabWatcher.Related(t, keep[i])) return true;
            return false;
        }
    }
}
