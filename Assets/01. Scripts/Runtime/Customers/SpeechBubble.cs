using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 손님 머리 위 말풍선 (2026-11-18 기기 테스트 피드백: 손님이 말을 안 해 단조로움). 대사는 CustomerDirector가 정합니다.
    /// 머리 뼈를 따라다니고, 플레이어 머리 쪽을 봅니다(WorldToast와 같은 방식). 일시정지 중에는 시간이 흐르지 않습니다.
    ///
    /// 4차 기기 피드백: 예전 바탕은 TMP &lt;mark&gt;였는데, mark는 글자 '위에' 덮이는 효과라 흰 바탕이 글자를 가려 읽히지 않았음.
    /// → 만화식 구름 말풍선을 글자 '뒤에' 그립니다. 동그라미 스프라이트(코드로 만든 원 하나)를 겹쳐
    ///   몸통(타원) + 가장자리 뭉게(작은 원들) + 꼬리(머리 쪽 작은 원 2개)를 만들고, 같은 모양을 조금 크게 진한 색으로 깔아 테두리를 냅니다.
    ///   글자 길이에 맞춰 크기가 바뀌어도 모양이 깨지지 않습니다(이미지 에셋 불필요).
    /// </summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        const float FadeSeconds = 0.3f;
        // 손님 키를 줄여도 글자 크기는 같게: 크기는 월드 기준으로 맞춤 (3차 피드백으로 키움)
        const float FontSize = 0.4f;
        const float MaxTextWidth = 0.8f;
        // 이 거리까지는 기본 크기, 더 멀면 거리에 비례해 키움 (최대 MaxDistanceScale배)
        const float ReferenceDistance = 1.5f;
        const float MaxDistanceScale = 2.5f;
        const float HeadClearance = 0.12f; // 머리 뼈 → 꼬리 끝

        // 구름 모양 (로컬 단위 = m, 거리 배율 적용 전)
        const float PadX = 0.06f, PadY = 0.045f;  // 글자 둘레 여백
        const float Outline = 0.012f;             // 테두리 두께
        const float TailHeight = 0.07f;           // 꼬리 영역 높이
        const int MaxBumps = 14;

        static readonly Color FillColor = Color.white;
        static readonly Color OutlineColor = new Color(0.16f, 0.13f, 0.22f);
        static readonly Color TextColor = new Color(0.1f, 0.1f, 0.22f);
        static Sprite circle;

        TextMeshPro text;
        Transform cloud;
        readonly List<SpriteRenderer> fills = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> outlines = new List<SpriteRenderer>();
        Transform head;
        Transform body;
        float left;
        float duration;

        /// <summary>일시정지 판단 (CustomerAgent가 넣어 줌)</summary>
        public System.Func<bool> IsPaused { get; set; }

        /// <summary>손님 오브젝트에 말풍선을 만듭니다. head가 없으면 몸 위 1.6m에 띄웁니다.</summary>
        public static SpeechBubble Create(Transform customer, Transform head, TMP_FontAsset font)
        {
            var go = new GameObject("SpeechBubble");
            // 손님이 사라질 때 함께 사라지게 자식으로 둠. 키 맞춤 배율은 Follow에서 되돌림
            go.transform.SetParent(customer, false);
            var bubble = go.AddComponent<SpeechBubble>();
            bubble.head = head;
            bubble.body = customer;

            // 구름: 원 스프라이트 (몸통 1 + 뭉게 MaxBumps + 꼬리 2) × (테두리 · 채움)
            bubble.cloud = new GameObject("Cloud").transform;
            bubble.cloud.SetParent(go.transform, false);
            for (int i = 0; i < MaxBumps + 3; i++)
            {
                bubble.outlines.Add(Circle(bubble.cloud, "Outline", OutlineColor, 0, 0.002f));
                bubble.fills.Add(Circle(bubble.cloud, "Fill", FillColor, 1, 0f));
            }

            // 글자: 구름보다 플레이어 쪽(-Z)으로 살짝 앞, 그리는 순서도 위
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            bubble.text = textGo.AddComponent<TextMeshPro>();
            if (font != null) bubble.text.font = font;
            bubble.text.fontSize = FontSize;
            bubble.text.alignment = TextAlignmentOptions.Center;
            bubble.text.textWrappingMode = TextWrappingModes.Normal;
            bubble.text.color = TextColor;
            bubble.text.GetComponent<MeshRenderer>().sortingOrder = 2;
            ((RectTransform)textGo.transform).sizeDelta = new Vector2(MaxTextWidth, 0.5f);

            go.SetActive(false);
            return bubble;
        }

        static SpriteRenderer Circle(Transform parent, string name, Color color, int order, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = CircleSprite();
            r.color = color;
            r.sortingOrder = order;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        // 지름 1(로컬 단위)짜리 부드러운 원. 모든 말풍선이 함께 씀
        static Sprite CircleSprite()
        {
            if (circle != null) return circle;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float c = (size - 1) / 2f, rad = size / 2f - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    byte a = (byte)(Mathf.Clamp01(rad - d + 0.5f) * 255f); // 가장자리 1px 부드럽게
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return circle;
        }

        public void Say(string message, float seconds)
        {
            if (string.IsNullOrEmpty(message)) return;
            text.text = message;
            duration = Mathf.Max(0.5f, seconds);
            left = duration;
            gameObject.SetActive(true);
            Layout();
            SetAlpha(1f);
            Follow();
        }


        // 글자 크기에 맞춰 구름을 배치. 원점 = 꼬리 끝(머리 바로 위), 위로 꼬리 → 몸통
        void Layout()
        {
            text.ForceMeshUpdate();
            Vector2 size = text.GetRenderedValues(true);
            float w = Mathf.Max(0.12f, size.x) + PadX * 2f;
            float h = Mathf.Max(0.05f, size.y) + PadY * 2f;
            float cy = TailHeight + h / 2f;
            text.transform.localPosition = new Vector3(0f, cy, -0.004f);

            int used = 0;
            // 몸통: 글자를 덮는 타원 (모서리를 둥글게 채우려고 조금 크게)
            Place(used++, new Vector2(0f, cy), new Vector2(w * 1.12f, h * 1.25f));

            // 뭉게: 타원 둘레에 작은 원 (가로가 길수록 개수 많게)
            float bump = Mathf.Clamp(h * 0.55f, 0.05f, 0.12f);
            int n = Mathf.Clamp(Mathf.RoundToInt((w + h) * 2f / (bump * 0.8f)), 6, MaxBumps);
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f) / n * Mathf.PI * 2f;
                var p = new Vector2(Mathf.Cos(a) * w * 0.5f, cy + Mathf.Sin(a) * h * 0.5f);
                float s = bump * (i % 2 == 0 ? 1f : 0.82f); // 크기를 번갈아 바꿔 구름처럼
                Place(used++, p, new Vector2(s, s));
            }

            // 꼬리: 몸통 아래에서 머리 쪽으로 작아지는 원 2개 (생각 · 말 구름 꼬리)
            Place(used++, new Vector2(-w * 0.12f, TailHeight * 0.75f), Vector2.one * 0.04f);
            Place(used++, new Vector2(-w * 0.18f, TailHeight * 0.2f), Vector2.one * 0.024f);

            for (int i = used; i < fills.Count; i++)
            {
                fills[i].enabled = false;
                outlines[i].enabled = false;
            }
        }

        void Place(int i, Vector2 center, Vector2 diameter)
        {
            if (i >= fills.Count) return;
            Transform f = fills[i].transform, o = outlines[i].transform;
            f.localPosition = new Vector3(center.x, center.y, 0f);
            f.localScale = new Vector3(diameter.x, diameter.y, 1f);
            o.localPosition = new Vector3(center.x, center.y, 0.002f);
            o.localScale = new Vector3(diameter.x + Outline * 2f, diameter.y + Outline * 2f, 1f);
            fills[i].enabled = true;
            outlines[i].enabled = true;
        }

        void SetAlpha(float a)
        {
            text.alpha = a;
            foreach (SpriteRenderer r in fills) { Color c = r.color; c.a = a; r.color = c; }
            foreach (SpriteRenderer r in outlines) { Color c = r.color; c.a = a; r.color = c; }
        }

        void LateUpdate()
        {
            if (IsPaused == null || !IsPaused()) left -= Time.deltaTime;
            if (left <= 0f) { gameObject.SetActive(false); return; }
            if (left < FadeSeconds) SetAlpha(Mathf.Clamp01(left / FadeSeconds));
            Follow();
        }

        void Follow()
        {
            Vector3 top = head != null ? head.position : body.position + Vector3.up * 1.6f;
            transform.position = top + Vector3.up * HeadClearance;

            // 부모(손님)의 키 맞춤 배율을 되돌려 글자 크기를 일정하게
            Vector3 s = body.lossyScale;
            Vector3 unscale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f / Mathf.Max(0.01f, s.z));

            // 글자 · 스프라이트 앞면은 -Z 쪽에서 보이므로 forward = 카메라 → 말풍선 방향
            Camera cam = Camera.main;
            if (cam == null)
            {
                transform.localScale = unscale;
                return;
            }
            Vector3 dir = transform.position - cam.transform.position;
            // 멀리 있는 손님 말풍선도 읽히게: 보이는 크기가 거의 일정하도록 거리에 비례해 키움 (원점이 꼬리 끝이라 위로만 커짐)
            float distanceScale = Mathf.Clamp(dir.magnitude / ReferenceDistance, 1f, MaxDistanceScale);
            transform.localScale = unscale * distanceScale;
            dir.y = 0f; // 수평으로만 돌림 (위아래로 기울면 읽기 어려움)
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }
}
