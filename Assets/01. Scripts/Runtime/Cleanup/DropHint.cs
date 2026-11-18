using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// "여기에 놓으세요" 표시 (3차 기기 피드백: 쓰레기 · 팩을 주워도 어디에 둘지 몰랐음).
    /// 청소 물건을 들고 있는 동안만 보이고, 놓으면 들어가는 범위에 오면 초록으로 바뀝니다.
    /// 모양(바닥 고리 · 떠 있는 표식 · 글자)은 씬 빌더(CleanupBuilder)가 만들어 연결하고, 이 컴포넌트는 켜고 끄기 · 색 · 흔들림만 맡습니다.
    /// </summary>
    public sealed class DropHint : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");

        [Tooltip("표시 전체 (켜고 끔)")]
        [SerializeField] GameObject visual;
        [Tooltip("색이 바뀌는 부분 (고리 · 표식 · 빈 칸 윤곽)")]
        [SerializeField] Renderer[] tinted;
        [Tooltip("위아래로 흔들리는 표식 (없어도 됨)")]
        [SerializeField] Transform bobber;
        [SerializeField] Color idleColor = new Color(1f, 0.78f, 0.2f);
        [SerializeField] Color readyColor = new Color(0.3f, 1f, 0.45f);
        [SerializeField] float bobHeight = 0.05f;
        [SerializeField] float bobSpeed = 3f;

        MaterialPropertyBlock block;
        Vector3 bobberBase;
        bool shown;
        bool ready;

        public bool Shown => shown;

        public void Configure(GameObject visualRoot, Renderer[] tintedRenderers, Transform bobbingMarker)
        {
            visual = visualRoot;
            tinted = tintedRenderers;
            bobber = bobbingMarker;
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (bobber != null) bobberBase = bobber.localPosition;
            if (visual != null) visual.SetActive(false);
        }

        public void SetShown(bool on)
        {
            if (shown == on) return;
            shown = on;
            if (visual != null) visual.SetActive(on);
            if (on) ApplyColor();
        }

        public void SetReady(bool on)
        {
            if (ready == on) return;
            ready = on;
            ApplyColor();
        }

        void Update()
        {
            if (!shown) return;
            float wave = Mathf.Sin(Time.time * bobSpeed);
            if (bobber != null) bobber.localPosition = bobberBase + Vector3.up * (wave * bobHeight);
            ApplyColor();
        }

        // 기다리는 동안은 은은하게 깜빡, 들어가는 범위면 밝은 초록으로 고정
        void ApplyColor()
        {
            if (tinted == null) return;
            float pulse = ready ? 1f : 0.7f + 0.3f * Mathf.Sin(Time.time * bobSpeed * 2f);
            Color c = (ready ? readyColor : idleColor) * pulse;
            c.a = 1f;
            foreach (Renderer r in tinted)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, c);
                block.SetColor(LegacyColor, c);
                r.SetPropertyBlock(block);
            }
        }
    }
}
