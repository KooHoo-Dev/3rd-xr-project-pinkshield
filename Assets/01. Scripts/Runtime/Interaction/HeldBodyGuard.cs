using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>
    /// 손에 쥔 물건 ↔ 플레이어 몸(CharacterController) 충돌 끄기 (11-20 플레이 뒤 BUG-022).
    /// 쥔 쓰레기를 허리 아래로 들면 발 앞의 "턱"이 되어 몸이 그 위로 올라서고, 그대로 쓰레기통 위에 서던 문제.
    /// 리그 손(상호작용기)이 무엇이든 쥐는 동안 그 물건의 충돌체와 몸의 충돌을 끄고, 놓은 뒤 몸과 떨어지면 다시 켭니다
    /// (겹친 채 켜면 물리가 물건을 튕겨 냄). 쥐기 전부터 꺼져 있던 짝(대걸레: Broom이 늘 끔)은 건드리지 않습니다.
    /// 씬을 바꾸지 않도록 장면이 열리면 몸 오브젝트에 스스로 붙습니다.
    /// </summary>
    public sealed class HeldBodyGuard : MonoBehaviour
    {
        const float ApartMargin = 0.05f; // 몸 경계와 이만큼 떨어지면 충돌을 다시 켬

        CharacterController body;
        readonly List<XRBaseInteractor> hands = new List<XRBaseInteractor>();
        readonly HashSet<Collider> ignored = new HashSet<Collider>(); // 이 컴포넌트가 끈 것만
        readonly HashSet<Collider> held = new HashSet<Collider>();
        readonly List<Collider> waiting = new List<Collider>();       // 놓았지만 아직 몸과 겹침

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var cc = FindAnyObjectByType<CharacterController>();
            if (cc != null && cc.GetComponent<HeldBodyGuard>() == null) cc.gameObject.AddComponent<HeldBodyGuard>();
        }

        void Awake() => body = GetComponent<CharacterController>();

        void OnEnable()
        {
            // 리그 아래의 손만 (가게 소켓 같은 다른 상호작용기는 제외)
            Transform rig = transform.root;
            foreach (XRBaseInteractor i in rig.GetComponentsInChildren<XRBaseInteractor>(true))
            {
                if (i is XRSocketInteractor) continue;
                i.selectEntered.AddListener(OnGrab);
                i.selectExited.AddListener(OnRelease);
                hands.Add(i);
            }
        }

        void OnDisable()
        {
            foreach (XRBaseInteractor i in hands)
            {
                if (i == null) continue;
                i.selectEntered.RemoveListener(OnGrab);
                i.selectExited.RemoveListener(OnRelease);
            }
            hands.Clear();
        }

        void OnGrab(SelectEnterEventArgs args)
        {
            if (body == null) return;
            foreach (Collider c in args.interactableObject.colliders)
            {
                if (c == null || c.isTrigger) continue;
                waiting.Remove(c);
                held.Add(c);
                if (ignored.Contains(c) || Physics.GetIgnoreCollision(body, c)) continue; // 이미 꺼짐 (대걸레 등)
                Physics.IgnoreCollision(body, c, true);
                ignored.Add(c);
            }
        }

        void OnRelease(SelectExitEventArgs args)
        {
            // 다른 손이 아직 쥐고 있으면 그대로 (양손 쥐기)
            if (args.interactableObject.isSelected) return;
            foreach (Collider c in args.interactableObject.colliders)
            {
                if (c == null || !held.Remove(c)) continue;
                if (ignored.Contains(c) && !waiting.Contains(c)) waiting.Add(c);
            }
        }

        void Update()
        {
            if (waiting.Count == 0 || body == null) return;
            Bounds b = body.bounds;
            b.Expand(ApartMargin * 2f);
            for (int i = waiting.Count - 1; i >= 0; i--)
            {
                Collider c = waiting[i];
                bool active = c != null && c.enabled && c.gameObject.activeInHierarchy;
                if (active && b.Intersects(c.bounds)) continue;
                // 꺼진 충돌체는 Unity가 무시 설정을 스스로 지움 (켠 채로만 되돌릴 수 있음)
                if (active) Physics.IgnoreCollision(body, c, false);
                ignored.Remove(c);
                waiting.RemoveAt(i);
            }
        }
    }
}
