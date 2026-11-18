using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Tutorial
{
    /// <summary>튜토리얼 중 엉뚱한 물건을 잡았을 때 (확정 결정: 기본은 알려 주기)</summary>
    public enum OutsideGrabMode
    {
        Notify = 0, // 막지 않고 알려 주기
        Lock = 1,   // 대상 말고는 잡히지 않게
    }

    /// <summary>
    /// 튜토리얼의 "다른 물건 잠그기" 선택지 (확정 결정: 기본은 막지 않고 알려 주기, 잠그기는 인스펙터 선택지로만).
    /// 기다림 · 정지 단계 동안 대상과 그 짝(스캐너 · 계산대 물건)을 뺀 물건의 interactionLayers를 비워 잡히지 않게 하고,
    /// Unlock · OnDisable에서 반드시 원래 값으로 되돌립니다. 이미 손에 든 물건은 잠그지 않습니다(놓을 수 없게 되는 일 방지).
    /// 잠근 뒤 새로 생긴 물건(쓰레기 등)은 잠기지 않습니다.
    /// </summary>
    public sealed class InteractionLock : MonoBehaviour
    {
        readonly Dictionary<XRBaseInteractable, InteractionLayerMask> saved = new Dictionary<XRBaseInteractable, InteractionLayerMask>();

        public bool IsLocked => saved.Count > 0;

        /// <param name="keep">잠그지 않을 대상들 (그 자식 · 부모 물건도 함께 남김)</param>
        public void Lock(IReadOnlyList<Transform> keep)
        {
            Unlock();
            foreach (XRBaseInteractable it in FindObjectsByType<XRBaseInteractable>(FindObjectsSortMode.None))
            {
                if (it == null || it.isSelected || Keeps(it.transform, keep) || TutorialGrabWatcher.IsHelper(it.transform)) continue;
                saved[it] = it.interactionLayers;
                it.interactionLayers = default; // 아무 층도 아님 = 잡히지 않음
            }
        }

        public void Unlock()
        {
            foreach (KeyValuePair<XRBaseInteractable, InteractionLayerMask> kv in saved)
                if (kv.Key != null) kv.Key.interactionLayers = kv.Value;
            saved.Clear();
        }

        void OnDisable() => Unlock();

        static bool Keeps(Transform t, IReadOnlyList<Transform> keep)
        {
            for (int i = 0; i < keep.Count; i++)
                if (TutorialGrabWatcher.Related(t, keep[i])) return true;
            return false;
        }
    }
}
