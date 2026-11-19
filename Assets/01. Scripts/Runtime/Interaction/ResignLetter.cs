using CardShop.Cards;
using CardShop.Core;
using CardShop.Core.Phases;
using CardShop.Presentation;
using CardShop.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>
    /// 무한 모드 그만두기 (무한모드_설계 J4 Q4, 11-20 변경): 집 컴퓨터 책상 위 "사직서" 봉투.
    /// 레이로 가리키거나 손을 대고 트리거를 1.5초 꾹 누르면 도장이 찍히고 무한 모드 결과 판으로 갑니다.
    /// 누르는 동안 봉투 위 빨간 링이 차오르고 진동이 점점 세집니다. 시간 판정은 <see cref="HoldToSkipTimer"/>(테스트됨)가 합니다.
    /// 실수 방지: 가리키기 전부터 누르던 트리거는 한 번 떼야 셉니다. 손을 떼거나 봉투에서 벗어나면 처음부터.
    /// 자리: 씬 자리표 "Home/ResignLetter"가 있으면 거기(위 = 봉투 윗면, 앞 +Z = 글자 위쪽), 없으면 책상(Desk) 앞쪽 왼쪽 모서리.
    /// 씬을 다시 빌드하지 않도록 실행 중에 스스로 만듭니다. 무한 모드이면서 집일 때만 보입니다.
    /// </summary>
    public sealed class ResignLetter : PhaseListener
    {
        /// <summary>씬 자리표(<see cref="PlacementAnchor"/>) 키. 모형 크기 약 0.24 × 0.04 × 0.14 (가로 × 높이 × 세로)</summary>
        public const string AnchorKey = "Home/ResignLetter";
        const float HoldSeconds = 1.5f, Grace = 0.25f;
        const int RingSegments = 20;
        const float RingRadius = 0.03f;
        static readonly Vector3 PaperSize = new Vector3(0.22f, 0.004f, 0.12f);
        static readonly Vector3 SealCenter = new Vector3(0.068f, 0f, 0f);
        static readonly Color Paper = new Color(0.96f, 0.93f, 0.85f);
        static readonly Color RingOff = new Color(0.55f, 0.5f, 0.45f, 0.5f);
        static readonly Color Red = new Color(0.85f, 0.15f, 0.15f);

        GameObject letter;
        Transform body;
        XRSimpleInteractable interactable;
        SpriteRenderer[] ring;
        GameObject hint, stamp;
        readonly HoldToSkipTimer timer = new HoldToSkipTimer(HoldSeconds, Grace);
        XRBaseInputInteractor tracked;
        float nextPulse;
        bool done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (GameContext.Instance == null || FindAnyObjectByType<ResignLetter>() != null) return;
            new GameObject("ResignLetter").AddComponent<ResignLetter>();
        }

        protected override void Start()
        {
            if (Place()) Build(WorldUiKit.FindFont());
            base.Start();
            Refresh(); // OnEnable 때는 봉투가 없어 첫 갱신이 비었음 → 만든 뒤 바로 숨김 상태로
        }

        /// <summary>이 오브젝트(봉투 받침)의 자리: +Y = 봉투 윗면, +Z = 플레이어 반대쪽(글자 위쪽).</summary>
        bool Place()
        {
            PlacementAnchor anchor = PlacementAnchor.Get(AnchorKey);
            if (anchor != null) { transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation); return true; }
            Transform desk = FindByName("Desk");
            Renderer deskRenderer = desk != null ? desk.GetComponent<Renderer>() : null;
            if (deskRenderer == null) { Debug.LogWarning("[ResignLetter] Desk · 자리표 Home/ResignLetter가 없어 사직서를 둘 자리가 없습니다."); return false; }

            Bounds b = deskRenderer.bounds;
            Transform monitor = FindByName("Monitor");
            Vector3 front = monitor != null ? b.center - monitor.position : -desk.forward; // 모니터 반대쪽 = 플레이어 쪽
            front.y = 0f;
            front = front.sqrMagnitude > 1e-4f ? front.normalized : Vector3.back;
            Vector3 right = Vector3.Cross(Vector3.up, front); // 플레이어가 책상을 볼 때 왼쪽 (모니터 옆을 비워 둠)
            float halfDepth = Mathf.Abs(front.x) * b.extents.x + Mathf.Abs(front.z) * b.extents.z;
            float halfWidth = Mathf.Abs(right.x) * b.extents.x + Mathf.Abs(right.z) * b.extents.z;
            Vector3 pos = new Vector3(b.center.x, b.max.y, b.center.z) + front * (halfDepth - 0.1f) + right * Mathf.Max(0f, halfWidth - 0.18f);
            // 반듯하지 않게 살짝 비틀어 놓음 (책상 위에 던져 둔 느낌)
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-front, Vector3.up) * Quaternion.Euler(0f, -8f, 0f));
            return true;
        }

        static Transform FindByName(string name)
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t;
            return null;
        }

        void Build(TMP_FontAsset font)
        {
            letter = new GameObject("Letter");
            letter.transform.SetParent(transform, false);
            var box = letter.AddComponent<BoxCollider>(); // 얇은 봉투도 손 · 레이가 쉽게 닿게 두툼하게
            box.size = new Vector3(PaperSize.x + 0.02f, 0.04f, PaperSize.z + 0.02f);
            box.center = new Vector3(0f, 0.02f, 0f);
            interactable = letter.AddComponent<XRSimpleInteractable>();

            body = new GameObject("Body").transform;
            body.SetParent(letter.transform, false);
            GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paper.name = "Paper";
            Destroy(paper.GetComponent<Collider>());
            paper.transform.SetParent(body, false);
            paper.transform.localPosition = new Vector3(0f, PaperSize.y * 0.5f, 0f);
            paper.transform.localScale = PaperSize;
            var paperRenderer = paper.GetComponent<Renderer>();
            paperRenderer.sharedMaterial = HomeRoom.Tinted(paperRenderer.sharedMaterial, Paper);

            // 윗면에 눕힌 판 (+Y를 봄, 글자 위쪽 = +Z)
            Transform face = new GameObject("Face").transform;
            face.SetParent(body, false);
            face.localPosition = new Vector3(0f, PaperSize.y + 0.0005f, 0f);
            face.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Text(face, "Title", "사 직 서", new Vector3(-0.03f, 0f, 0f), 0.22f, new Color(0.15f, 0.12f, 0.1f), font, new Vector2(0.14f, 0.05f));

            ring = new SpriteRenderer[RingSegments];
            float tangent = 2f * Mathf.PI * RingRadius / RingSegments * 0.7f;
            for (int i = 0; i < RingSegments; i++)
            {
                float a = 90f - 360f * i / RingSegments; // 12시부터 시계 방향
                Vector3 p = SealCenter + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f) * RingRadius;
                ring[i] = RevealSprites.Quad(face, "Ring" + i, RevealSprites.Center, p, new Vector2(tangent, 0.006f), RingOff);
                ring[i].transform.localRotation = Quaternion.Euler(0f, 0f, a + 90f);
            }

            // 다 차면 찍히는 빨간 도장 (판보다 살짝 위 = -Z)
            stamp = new GameObject("Stamp");
            stamp.transform.SetParent(face, false);
            stamp.transform.localPosition = SealCenter - new Vector3(0f, 0f, 0.0003f);
            stamp.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            RevealSprites.Quad(stamp.transform, "Seal", RevealSprites.Center, Vector3.zero, new Vector2(0.045f, 0.045f), Red, 1);
            Text(stamp.transform, "Text", "제출", new Vector3(0f, 0f, -0.0003f), 0.14f, Color.white, font, new Vector2(0.05f, 0.03f));
            stamp.SetActive(false);

            // 가리키면 봉투 위에 뜨는 안내 (플레이어 쪽을 봄)
            hint = Text(letter.transform, "Hint", $"<mark={UiTheme.Mark} padding=\"6,6,3,3\"> 트리거 꾹 — 근무 끝내기 </mark>",
                new Vector3(0f, 0.09f, 0f), 0.08f, Color.white, font, new Vector2(0.35f, 0.04f)).gameObject;
            hint.SetActive(false);
        }

        static TMP_Text Text(Transform parent, string name, string text, Vector3 local, float size, Color color, TMP_FontAsset font, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var t = go.AddComponent<TextMeshPro>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size; // 1 ≈ 10cm
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            ((RectTransform)go.transform).sizeDelta = box;
            return t;
        }

        protected override void Refresh()
        {
            if (letter == null) return;
            bool show = CurrentPhase == GamePhaseId.Home && Context != null && Context.Session != null && Context.Session.Endless.Active;
            if (letter.activeSelf != show) letter.SetActive(show);
            done = false;
            tracked = null;
            timer.Update(false, Time.unscaledTime);
            stamp.SetActive(false);
            SetRing(0f);
        }

        void Update()
        {
            if (letter == null || !letter.activeSelf || done) return;
            float now = Time.unscaledTime;
            XRBaseInputInteractor hand = HoveringHand();
            if (hand != tracked)
            {
                tracked = hand;
                timer.Update(false, now);
                if (hand != null && hand.activateInput.ReadIsPerformed()) timer.RequireRelease(); // 가리키기 전부터 누르던 트리거는 무시
            }
            bool fired = timer.Update(hand != null && hand.activateInput.ReadIsPerformed(), now);

            body.localPosition = Vector3.Lerp(body.localPosition, new Vector3(0f, hand != null ? 0.015f : 0f, 0f), 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            if (hint.activeSelf != (hand != null)) hint.SetActive(hand != null);

            if (fired) { Submit(hand); return; }
            bool charging = timer.IsCharging(now);
            float progress = charging ? timer.Progress(now) : 0f;
            SetRing(progress);
            if (charging && now >= nextPulse)
            {
                hand.SendHapticImpulse(Mathf.Lerp(0.05f, 0.5f, progress), 0.1f); // 누를수록 세짐
                nextPulse = now + 0.09f;
            }
        }

        /// <summary>봉투를 가리키는(레이) · 닿은(손) 컨트롤러. 이미 쓰던 손을 먼저 유지합니다. 손가락 Poke는 트리거가 없어 제외.</summary>
        XRBaseInputInteractor HoveringHand()
        {
            XRBaseInputInteractor first = null;
            foreach (var h in interactable.interactorsHovering)
            {
                if (!(h is XRBaseInputInteractor input)) continue;
                if (input == tracked) return input;
                if (first == null) first = input;
            }
            return first;
        }

        void SetRing(float progress)
        {
            int lit = Mathf.FloorToInt(progress * RingSegments);
            for (int i = 0; i < ring.Length; i++) ring[i].color = i < lit ? Red : RingOff;
        }

        void Submit(XRBaseInputInteractor hand)
        {
            done = true;
            SetRing(1f);
            stamp.SetActive(true);
            hint.SetActive(false);
            Sfx.PlayFirst(new[] { SoundId.ResignStamp, SoundId.BagPut }, letter.transform.position, 0); // 도장 소리 칸이 비면 봉투 소리
            if (hand != null) hand.SendHapticImpulse(0.9f, 0.2f);
            if (Flow != null) Flow.GetPhase<HomePhase>(GamePhaseId.Home)?.Resign();
        }
    }
}
