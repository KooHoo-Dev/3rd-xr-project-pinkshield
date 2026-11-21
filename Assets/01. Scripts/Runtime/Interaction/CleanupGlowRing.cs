using CardShop.UI;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Interaction
{
    /// <summary>
    /// 치울 물건(바닥 쓰레기 · 꽂이에서 빠진 팩) 아래 바닥에 깔리는 연주황 빛 고리 (11-21 2차 F2).
    /// 외곽선 껍데기는 메시를 살짝 키우는 방식이라, 바닥에 납작하게 붙은 신문지 같은 물건은 테가 바닥에 묻혀 안 보였음 → 바닥 위 1cm에 고리.
    /// 외곽선의 연주황 숨쉬기와 같은 2초 박자. 쥐고 있거나 치운 뒤에는 숨김. InteractableOutline이 치울 물건에만 붙입니다.
    /// 고리는 물건의 자식이 아니라 따로 두고(물건이 굴러도 바닥에 평평하게) 매 프레임 물건 아래로 따라감.
    /// </summary>
    public sealed class CleanupGlowRing : MonoBehaviour
    {
        static readonly Color RingColor = new Color(1f, 0.6f, 0.28f);
        const float BreathSeconds = 2f;
        const float MinDiameter = 0.28f;
        const float FloorLift = 0.01f;
        const float CircleUnit = 1.28f; // UiSprites.Ring 한 장 크기 (128px, 100px = 1유닛)

        System.Func<bool> isCleanup;
        XRBaseInteractable interactable;
        SpriteRenderer ring;

        public void Init(System.Func<bool> cleanup) => isCleanup = cleanup;

        void Start()
        {
            interactable = GetComponent<XRBaseInteractable>();
            var go = new GameObject("CleanupGlowRing");
            ring = go.AddComponent<SpriteRenderer>();
            ring.sprite = UiSprites.Ring(10);
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 눕힘: 위에서 보임
            go.SetActive(false);
        }

        void OnDestroy()
        {
            if (ring != null) Destroy(ring.gameObject);
        }

        void LateUpdate()
        {
            if (ring == null) return;
            bool show = isCleanup != null && isCleanup() && (interactable == null || !interactable.isSelected) && TryBounds(out Bounds b);
            if (ring.gameObject.activeSelf != show) ring.gameObject.SetActive(show);
            if (!show) return;

            TryBounds(out b);
            float d = Mathf.Max(MinDiameter, Mathf.Max(b.size.x, b.size.z) * 1.5f) / CircleUnit;
            ring.transform.position = new Vector3(b.center.x, b.min.y + FloorLift, b.center.z);
            ring.transform.localScale = new Vector3(d, d, 1f);
            float breath = 0.5f + 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f / BreathSeconds);
            Color c = RingColor;
            c.a = Mathf.Lerp(0.45f, 0.95f, breath);
            ring.color = c;
        }

        bool TryBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r.name == "OutlineShell" || !r.enabled) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
