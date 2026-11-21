using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace CardShop.Interaction
{
    /// <summary>
    /// 손으로 쓸 수 있는 모든 물건에 외곽선 (11-20 피드백): 평소 = 얇은 흰 테, 쥐고 있으면 = 굵은 노란 테.
    /// 튜토리얼 강조(분홍, OutlineMarker)와 같은 셰이더(CardShop/Outline)를 쓰지만, 원래 재질 배열을 건드리지 않도록
    /// 렌더러마다 같은 메시를 그리는 자식 "껍데기"를 따로 둠 (서브메시가 여럿인 카드도 전체에 테가 생김).
    /// 카드 · 팩처럼 아주 얇은 물건은 법선으로 부풀리면 테가 안 보여서 메시 가운데에서 몇 % 키우는 방식(_Grow).
    /// XRInteractionManager에 등록되는 물건(나중에 생기는 팩 · 카드 · 쓰레기 포함)에 스스로 붙음. 순간이동 바닥은 제외.
    /// 11-21 피드백 O1 · O2: 치울 물건(바닥 쓰레기 · 흐트러진 팩)은 연주황 굵은 테가 2초마다 숨쉬고, 누르는 버튼은 납작해서 테를 2배로.
    /// 색 · 굵기는 껍데기마다 MaterialPropertyBlock으로 덮어씀(재질은 셰이더 방식 고르기만).
    /// </summary>
    public sealed class InteractableOutline : MonoBehaviour
    {
        const float ThinThreshold = 0.006f; // 이보다 얇으면(m) 가운데에서 키우는 방식
        const float MaxSize = 2.5f;         // 이보다 큰 물건(바닥 · 벽 크기)은 테를 두르지 않음

        static readonly Color Idle = new Color(0.96f, 0.96f, 0.96f);
        static readonly Color Held = new Color(1f, 0.82f, 0.2f);
        static readonly Color Cleanup = new Color(1f, 0.6f, 0.28f);  // 치울 것: 튜토리얼 분홍 · 쥔 노랑과 겹치지 않는 연주황
        const float CleanupBreathSeconds = 2f;
        const float IdleWidth = 0.003f, HeldWidth = 0.006f, IdleGrow = 0.05f, HeldGrow = 0.09f;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int WidthId = Shader.PropertyToID("_Width");
        static readonly int GrowId = Shader.PropertyToID("_Grow");
        static Material idleHull, heldHull, idleThin, heldThin;
        static readonly int CenterId = Shader.PropertyToID("_Center");

        struct Shell
        {
            public Renderer Source;
            public MeshRenderer Renderer;
            public bool Thin;
        }

        readonly List<Shell> shells = new List<Shell>();
        XRBaseInteractable interactable;
        bool held, button, cleanupShown;
        System.Func<bool> isCleanup; // 지금 치울 물건인가 (쓰레기 · 꽂이에서 빠진 팩), 아니면 null
        MaterialPropertyBlock block;

        // ------------------------------------------------------------------ 스스로 붙기

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!MakeMaterials()) return;
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

        static void Attach(XRBaseInteractable i)
        {
            if (i == null || i is BaseTeleportationInteractable || i.GetComponent<InteractableOutline>() != null) return;
            i.gameObject.AddComponent<InteractableOutline>();
        }

        static bool MakeMaterials()
        {
            if (idleHull != null) return true;
            Shader shader = Resources.Load<Shader>("CardShopOutline");
            if (shader == null || !shader.isSupported) return false;
            idleHull = Make(shader, "OutlineIdle", Idle, 0.003f, 0f);
            heldHull = Make(shader, "OutlineHeld", Held, 0.006f, 0f);
            idleThin = Make(shader, "OutlineIdleThin", Idle, 0f, 0.05f);
            heldThin = Make(shader, "OutlineHeldThin", Held, 0f, 0.09f);
            return true;
        }

        static Material Make(Shader shader, string name, Color color, float width, float grow)
        {
            var m = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            m.SetColor("_Color", color);
            m.SetFloat("_Width", width);
            m.SetFloat("_Grow", grow);
            return m;
        }

        // ------------------------------------------------------------------ 껍데기

        void Start()
        {
            interactable = GetComponent<XRBaseInteractable>();
            if (interactable == null) { Destroy(this); return; }
            interactable.selectEntered.AddListener(OnSelect);
            interactable.selectExited.AddListener(OnSelect);
            button = GetComponent<PokeButton>() != null;
            if (TryGetComponent(out CardShop.Cleanup.TrashSpot trash)) isCleanup = () => trash != null && !trash.IsDone;
            else if (TryGetComponent(out CardShop.Cleanup.RackPack pack)) isCleanup = () => pack != null && !pack.InRack;
            Build();
            held = interactable.isSelected;
            Paint();
        }

        void OnDestroy()
        {
            if (interactable == null) return;
            interactable.selectEntered.RemoveListener(OnSelect);
            interactable.selectExited.RemoveListener(OnSelect);
        }

        void Build()
        {
            foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponent<TMP_Text>() != null || r.name == "OutlineShell") continue;
                // 안쪽에 따로 잡히는 물건이 있으면 그 물건이 자기 테를 가짐
                XRBaseInteractable owner = r.GetComponentInParent<XRBaseInteractable>(true);
                if (owner != null && owner != interactable) continue;
                if (!r.TryGetComponent(out MeshFilter f) || f.sharedMesh == null) continue;
                Vector3 size = Vector3.Scale(f.sharedMesh.bounds.size, r.transform.lossyScale);
                float big = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                float small = Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                if (big > MaxSize || big < 0.01f) continue;

                var go = new GameObject("OutlineShell");
                go.transform.SetParent(r.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = f.sharedMesh;
                var shell = go.AddComponent<MeshRenderer>();
                shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shell.receiveShadows = false;
                var mpb = new MaterialPropertyBlock();
                mpb.SetVector(CenterId, f.sharedMesh.bounds.center);
                shell.SetPropertyBlock(mpb);
                shells.Add(new Shell { Source = r, Renderer = shell, Thin = small < ThinThreshold });
            }
        }

        void OnSelect(BaseInteractionEventArgs _)
        {
            held = interactable != null && interactable.isSelected;
            Paint();
        }

        void Paint()
        {
            foreach (Shell s in shells)
            {
                if (s.Renderer == null) continue;
                Material m = s.Thin ? (held ? heldThin : idleThin) : (held ? heldHull : idleHull);
                int subs = Mathf.Max(1, s.Renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount);
                var mats = new Material[subs]; // 서브메시 모두에 같은 테
                for (int i = 0; i < subs; i++) mats[i] = m;
                s.Renderer.sharedMaterials = mats;
            }
            cleanupShown = !held && isCleanup != null && isCleanup();
            Tint(cleanupShown ? 1f : 0f);
        }

        // 껍데기마다 색 · 굵기 (breath 0~1 = 치울 것 숨쉬기 밝기, 치울 것이 아니면 무시)
        void Tint(float breath)
        {
            block ??= new MaterialPropertyBlock();
            bool thick = held || button || cleanupShown;
            Color color = held ? Held : cleanupShown ? Color.Lerp(Cleanup * 0.55f, Cleanup, breath) : Idle;
            color.a = 1f;
            foreach (Shell s in shells)
            {
                if (s.Renderer == null) continue;
                s.Renderer.GetPropertyBlock(block); // _Center는 그대로 둠
                block.SetColor(ColorId, color);
                block.SetFloat(WidthId, s.Thin ? 0f : thick ? HeldWidth : IdleWidth);
                block.SetFloat(GrowId, s.Thin ? (thick ? HeldGrow : IdleGrow) : 0f);
                s.Renderer.SetPropertyBlock(block);
            }
        }

        // 원래 렌더러가 꺼지면(숨긴 팩 · 연출 중) 테도 같이
        void LateUpdate()
        {
            // 치울 물건: 꽂이에 다시 꽂히거나 버려지면 평소 테로, 아니면 2초마다 숨쉬기
            bool cleanup = !held && isCleanup != null && isCleanup();
            if (cleanup != cleanupShown) { cleanupShown = cleanup; Tint(cleanup ? 1f : 0f); }
            if (cleanupShown) Tint(0.5f + 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f / CleanupBreathSeconds));

            for (int i = 0; i < shells.Count; i++)
            {
                Shell s = shells[i];
                if (s.Renderer == null || s.Source == null) continue;
                bool on = s.Source.enabled;
                if (s.Renderer.enabled != on) s.Renderer.enabled = on;
            }
        }
    }
}
