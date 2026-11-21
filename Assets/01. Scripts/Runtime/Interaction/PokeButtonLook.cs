using CardShop.UI;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Interaction
{
    /// <summary>
    /// 가게 누르는 버튼의 "슬라임 젤리 버튼" 겉모습 (폴리싱 설계 P1 A안, 11-22).
    /// 진보라 받침 + 금 테 위에 슬라임 돔 캡(광택) · 앞에 비스듬한 명판. 누르면 캡이 납작해졌다가 통 튀어 오르고, 가리키면 밝아집니다.
    /// 씬을 다시 만들지 않도록 실행할 때 스스로 만듭니다: 원래 큐브 캡(Cap)은 숨기고, 그 재질을 복사해 색만 씁니다
    /// (런타임에 셰이더를 찾지 않음 — 빌드에서 빠질 수 있어서). 외곽선(InteractableOutline)보다 먼저(Awake) 만들어 새 몸에 테가 생깁니다.
    /// <see cref="PokeButton"/>이 붙이고, 집의 불 끄고 자기 스위치는 붙이지 않습니다.
    /// </summary>
    [RequireComponent(typeof(PokeButton))]
    public sealed class PokeButtonLook : MonoBehaviour
    {
        const float BaseRadius = 0.045f, BaseHeight = 0.012f;
        const float DomeRadius = 0.032f, DomeHeight = 0.03f;
        static readonly Color BaseColor = new Color(0.2f, 0.15f, 0.31f);
        static readonly Color RimGold = new Color(0.91f, 0.76f, 0.42f, 0.9f);
        static readonly Color PlateColor = new Color(0.23f, 0.17f, 0.35f, 0.96f);
        static readonly Color PlateText = new Color(1f, 0.97f, 0.92f);

        static Mesh domeMesh, discMesh;

        Transform dome;
        Material domeMat;
        Color color;
        SpriteRenderer rim;
        float squash;      // 0 = 그대로, 1 = 다 눌림
        float bounceStart = -1f;
        bool pressed, hovered;

        /// <summary>버튼 몸 색 (둘레 빛 판이 같은 색을 씀)</summary>
        public Color BodyColor => color;

        void Awake()
        {
            Transform cap = transform.Find("Cap");
            var capRenderer = cap != null ? cap.GetComponent<MeshRenderer>() : null;
            if (capRenderer == null || capRenderer.sharedMaterial == null) { enabled = false; return; }
            color = capRenderer.sharedMaterial.color;
            capRenderer.enabled = false; // 눌림 위치(pressVisual)는 그대로 Cap이 맡고, 보이는 몸만 바꿈

            Material baseMat = new Material(capRenderer.sharedMaterial) { name = "ButtonBase", color = BaseColor };
            SetSmoothness(baseMat, 0.35f);
            domeMat = new Material(capRenderer.sharedMaterial) { name = "ButtonJelly", color = color };
            SetSmoothness(domeMat, 0.85f);

            Part("JellyBase", Disc(), baseMat, new Vector3(0f, 0f, 0f), new Vector3(BaseRadius, BaseHeight, BaseRadius));
            dome = Part("JellyCap", Dome(), domeMat, new Vector3(0f, BaseHeight, 0f), new Vector3(DomeRadius, DomeHeight, DomeRadius)).transform;

            // 받침 윗면의 금 테 (누워 있는 고리)
            rim = Flat("JellyRim", UiSprites.Ring(6), new Vector3(0f, BaseHeight + 0.0006f, 0f), BaseRadius * 1.9f, RimGold);
            // 캡 위쪽 광택: 눕힌 판을 앞쪽으로 조금 기울여 젤리 윗면에 하이라이트처럼
            SpriteRenderer gloss = Flat("JellyGloss", UiSprites.SoftDot(), Vector3.zero, 0.028f, new Color(1f, 1f, 1f, 0.5f));
            gloss.transform.SetParent(dome, false);
            gloss.transform.localPosition = new Vector3(-0.25f, 0.97f, -0.25f); // 돔 로컬 (반지름 1 · 높이 1 기준)
            gloss.transform.localRotation = Quaternion.Euler(90f - 25f, 0f, 0f);
            gloss.transform.localScale = new Vector3(gloss.transform.localScale.x / DomeRadius, gloss.transform.localScale.y * 0.55f / DomeRadius, 1f);

            Nameplate();

            var button = GetComponent<PokeButton>();
            button.PressChanged += OnPressChanged;
            var interactable = GetComponent<XRSimpleInteractable>();
            interactable.hoverEntered.AddListener(_ => hovered = true);
            interactable.hoverExited.AddListener(_ => hovered = interactable.isHovered);
        }

        void OnPressChanged(bool down)
        {
            if (pressed && !down) bounceStart = Time.unscaledTime;
            pressed = down;
        }

        void Update()
        {
            if (dome == null) return;
            // 누름: 0.09초에 납작 / 뗌: 0.28초 동안 위로 늘었다가 제자리 (미리 보기와 같은 모양)
            float sx = 1f, sy = 1f;
            if (pressed)
            {
                squash = Mathf.MoveTowards(squash, 1f, Time.unscaledDeltaTime / 0.09f);
                sx = 1f + 0.12f * squash;
                sy = 1f - 0.4f * squash;
            }
            else if (bounceStart >= 0f)
            {
                float t = (Time.unscaledTime - bounceStart) / 0.28f;
                if (t >= 1f) { bounceStart = -1f; squash = 0f; }
                else
                {
                    float from = squash;
                    float wobble = t < 0.35f ? Mathf.Lerp(-from, 0.3f, t / 0.35f) : 0.3f * Mathf.Cos((t - 0.35f) / 0.65f * Mathf.PI * 1.5f) * (1f - t);
                    sy = 1f + 0.4f * wobble;
                    sx = 1f - 0.12f * wobble;
                }
            }
            dome.localScale = new Vector3(DomeRadius * sx, DomeHeight * sy, DomeRadius * sx);

            float glow = hovered ? 1f : 0f;
            domeMat.color = Color.Lerp(color, Color.Lerp(color, Color.white, 0.25f), glow);
            rim.color = Color.Lerp(RimGold, color, glow);
        }

        // ------------------------------------------------------------------ 만들기

        GameObject Part(string partName, Mesh mesh, Material mat, Vector3 localPos, Vector3 scale)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        SpriteRenderer Flat(string partName, Sprite sprite, Vector3 localPos, float diameter, Color c)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 위를 봄
            float unit = diameter / sprite.bounds.size.x;
            go.transform.localScale = new Vector3(unit, unit, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = c;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return sr;
        }

        // 원래 떠 있던 글자(Label)를 받침 앞쪽에 비스듬히 선 명판 위로 옮김. 앞쪽 = 글자가 읽히는 쪽
        void Nameplate()
        {
            Transform label = transform.Find("Label");
            if (label == null || !label.TryGetComponent(out TextMeshPro text)) return;
            Vector3 toViewer = -label.forward;
            toViewer.y = 0f;
            if (toViewer.sqrMagnitude < 1e-4f) toViewer = -transform.forward;
            toViewer.Normalize();
            Quaternion face = Quaternion.LookRotation(-toViewer, Vector3.up) * Quaternion.Euler(35f, 0f, 0f); // 윗변이 뒤로 35° 누움 (위에서 내려다보기 좋게)

            var plate = new GameObject("Nameplate");
            plate.transform.SetParent(transform, false);
            plate.transform.SetPositionAndRotation(transform.position + toViewer * (BaseRadius + 0.014f) + Vector3.up * 0.016f, face);
            var bg = plate.AddComponent<SpriteRenderer>();
            bg.sprite = UiSprites.Rounded(32f / 0.006f); // 모서리 6mm
            bg.drawMode = SpriteDrawMode.Sliced;
            bg.size = new Vector2(0.11f, 0.03f);
            bg.color = PlateColor;
            var frame = new GameObject("Frame").AddComponent<SpriteRenderer>();
            frame.transform.SetParent(plate.transform, false);
            frame.transform.localPosition = new Vector3(0f, 0f, -0.0005f);
            frame.sprite = UiSprites.Frame(3, 32f / 0.006f);
            frame.drawMode = SpriteDrawMode.Sliced;
            frame.size = bg.size;
            frame.color = RimGold;

            label.SetParent(plate.transform, false);
            label.localPosition = new Vector3(0f, 0f, -0.001f);
            label.localRotation = Quaternion.identity;
            ((RectTransform)label).sizeDelta = new Vector2(0.1f, 0.024f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.05f;
            text.fontSizeMax = text.fontSize;
            text.color = PlateText;
            text.alignment = TextAlignmentOptions.Center;
        }

        static void SetSmoothness(Material m, float v)
        {
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", v);
        }

        // 반지름 1 · 높이 1 반구 (아래가 평평). 납작함은 Transform 배율로
        static Mesh Dome()
        {
            if (domeMesh != null) return domeMesh;
            const int Seg = 24, Rings = 8;
            var v = new System.Collections.Generic.List<Vector3>();
            var n = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            for (int r = 0; r <= Rings; r++)
            {
                float phi = Mathf.PI * 0.5f * r / Rings; // 0 = 바닥 둘레, π/2 = 꼭대기
                for (int s = 0; s <= Seg; s++)
                {
                    float th = Mathf.PI * 2f * s / Seg;
                    var p = new Vector3(Mathf.Cos(phi) * Mathf.Cos(th), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(th));
                    v.Add(p);
                    n.Add(p);
                }
            }
            for (int r = 0; r < Rings; r++)
                for (int s = 0; s < Seg; s++)
                {
                    int a = r * (Seg + 1) + s, b = a + Seg + 1;
                    tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            domeMesh = new Mesh { name = "JellyDome" };
            domeMesh.SetVertices(v);
            domeMesh.SetNormals(n);
            domeMesh.SetTriangles(tris, 0);
            domeMesh.RecalculateBounds();
            return domeMesh;
        }

        // 반지름 1 · 높이 1 원기둥 (옆면 + 윗면), 바닥은 카운터에 붙어 안 보임
        static Mesh Disc()
        {
            if (discMesh != null) return discMesh;
            const int Seg = 32;
            var v = new System.Collections.Generic.List<Vector3>();
            var n = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            for (int s = 0; s <= Seg; s++)
            {
                float th = Mathf.PI * 2f * s / Seg;
                var d = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
                v.Add(d); n.Add(d);
                v.Add(d + Vector3.up); n.Add(d);
            }
            for (int s = 0; s < Seg; s++)
            {
                int a = s * 2;
                tris.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 });
            }
            int center = v.Count;
            v.Add(Vector3.up); n.Add(Vector3.up);
            for (int s = 0; s <= Seg; s++)
            {
                float th = Mathf.PI * 2f * s / Seg;
                v.Add(new Vector3(Mathf.Cos(th), 1f, Mathf.Sin(th))); n.Add(Vector3.up);
            }
            for (int s = 0; s < Seg; s++) tris.AddRange(new[] { center, center + 2 + s, center + 1 + s });
            discMesh = new Mesh { name = "JellyBase" };
            discMesh.SetVertices(v);
            discMesh.SetNormals(n);
            discMesh.SetTriangles(tris, 0);
            discMesh.RecalculateBounds();
            return discMesh;
        }
    }
}
