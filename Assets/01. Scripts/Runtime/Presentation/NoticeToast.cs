using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using CardShop.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CardShop.Presentation
{
    /// <summary>
    /// 알림(<see cref="GameEvents.Notice"/>)을 플레이어 정면에 종이 쪽지로 띄웁니다 (UI 개편 C안 "알림").
    /// 지금까지 알림은 디버그 판에만 보여서 기기에서는 도둑 · 청소 · 퇴근 불가 같은 경고를 못 봤습니다.
    /// 모양은 <see cref="WorldToast"/>와 같은 종이 쪽지 + 왼쪽에 빨간 원 "!" 아이콘. 나타날 때 톡 튀어 오르고(0.85 → 1.05 → 1), 사라질 때 흐려집니다.
    /// 놓는 자리는 튜토리얼 자막과 같은 곳(자막보다 작게), 자막이 떠 있는 동안은 쉬고 닫힌 뒤 3초 안의 경고만 이어서 보여 줌 (11-20).
    /// 새 알림이 오면 이전 쪽지를 바로 바꾸고, 같은 문구가 1초 안에 또 오면 무시합니다.
    /// 씬이 열리면 스스로 붙습니다 (씬 파일을 고치지 않음). 디버그 판의 구독은 따로 그대로입니다.
    /// </summary>
    public sealed class NoticeToast : MonoBehaviour
    {
        // 자리 = 튜토리얼 자막과 같은 곳 (CaptionPlacement: 아래 12° · 왼쪽 18°). 안내가 늘 한 곳에서 나와 시선이 나뉘지 않게 (11-20 피드백)
        const float BaseScale = 1.4f;     // 자막보다 작게 (예전 2 = 혼잣말 크기의 0.7배)
        const float StaleSeconds = 3f;    // 자막 때문에 미뤄 둔 경고는 이 안에 자막이 닫히면 이어서, 더 지나면 버림 (그 순간에만 의미 있는 경고)
        const float PopSeconds = 0.18f;
        const float FadeSeconds = 0.3f;
        const float RepeatGuardSeconds = 1f;
        const float IconDiameter = 0.03f; // 쪽지 로컬 m
        static readonly Vector2 PlatePadding = new Vector2(0.035f, 0.012f); // 가로를 넓혀 왼쪽에 아이콘 자리를 둠

        GameContext context;
        Transform visual;       // 크기 · 보임을 다루는 쪽지 뿌리
        TextMeshPro text;
        TextPlate plate;
        SpriteRenderer iconCircle;
        SpriteRenderer iconCheck; // 좋은 소식 ✓ (글꼴에 ✓가 없을 수 있어 그림)
        TextMeshPro iconMark;
        string pending;           // 자막이 떠 있어 미뤄 둔 알림
        NoticeKind pendingKind;
        float pendingAt;
        Color iconColor = UiTheme.Danger; // 경고 = 빨강 !, 좋은 소식 = 초록 ✓ (NoticeRules)
        float shownAt, hideAt;
        string lastMessage;
        float lastMessageAt = -999f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (GameContext.Instance == null || FindAnyObjectByType<NoticeToast>(FindObjectsInactive.Include) != null) return;
            new GameObject("NoticeToast").AddComponent<NoticeToast>();
        }

        void Start()
        {
            context = GameContext.Instance;
            if (context != null) context.Events.Notice += OnNotice;
            Build();
        }

        void OnDestroy()
        {
            if (context != null) context.Events.Notice -= OnNotice;
        }

        void Build()
        {
            var root = new GameObject("Toast");
            visual = root.transform;
            visual.SetParent(transform, false);

            var body = new GameObject("Text", typeof(RectTransform));
            body.transform.SetParent(visual, false);
            text = body.AddComponent<TextMeshPro>();
            text.font = WorldUiKit.KoreanFont;
            text.fontSize = 0.25f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal; // 긴 안내는 두 줄로 (한 줄이면 1m 거리에서 시야 밖으로 나감)
            ((RectTransform)body.transform).sizeDelta = new Vector2(0.48f, 0.06f);
            text.color = UiTheme.Ink;
            plate = TextPlate.Attach(text, UiTheme.Paper, PlatePadding, 0.01f, UiTheme.Kraft, 0.002f);

            // 왼쪽 원 아이콘: 빨간 원 + 흰 "!" (종이 판보다 위에 그림)
            var circle = new GameObject("Icon");
            circle.transform.SetParent(visual, false);
            iconCircle = circle.AddComponent<SpriteRenderer>();
            iconCircle.sprite = UiSprites.Circle();
            iconCircle.sortingOrder = 3;
            iconCircle.shadowCastingMode = ShadowCastingMode.Off;
            circle.transform.localScale = Vector3.one * (IconDiameter / (iconCircle.sprite.rect.width / iconCircle.sprite.pixelsPerUnit));

            var check = new GameObject("Check");
            check.transform.SetParent(circle.transform, false);
            iconCheck = check.AddComponent<SpriteRenderer>();
            iconCheck.sprite = UiSprites.Check();
            iconCheck.sortingOrder = 4;
            iconCheck.shadowCastingMode = ShadowCastingMode.Off;
            check.transform.localScale = Vector3.one * 0.8f;

            var mark = new GameObject("Mark", typeof(RectTransform));
            mark.transform.SetParent(visual, false);
            ((RectTransform)mark.transform).sizeDelta = new Vector2(IconDiameter, IconDiameter);
            iconMark = mark.AddComponent<TextMeshPro>();
            iconMark.font = WorldUiKit.KoreanFont;
            UiFonts.Apply(iconMark);
            iconMark.fontSize = 0.28f;
            iconMark.alignment = TextAlignmentOptions.Center;
            iconMark.textWrappingMode = TextWrappingModes.NoWrap;
            iconMark.text = "!";
            iconMark.color = UiTheme.Text;
            var mr = mark.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 4;

            visual.gameObject.SetActive(false);
        }

        void OnNotice(string message)
        {
            if (visual == null) return;
            NoticeKind kind = NoticeRules.Classify(message);
            if (kind == NoticeKind.Hidden) return;
            float now = Time.unscaledTime;
            if (message == lastMessage && now - lastMessageAt < RepeatGuardSeconds) return; // 같은 문구 연타 무시
            lastMessage = message;
            lastMessageAt = now;

            // 안내는 한 번에 하나: 튜토리얼 자막이 떠 있으면 미뤄 둠 (튜토리얼이 같은 일을 이미 설명하는 중)
            if (CardShop.Tutorial.TutorialCaption.IsShowing)
            {
                pending = message;
                pendingKind = kind;
                pendingAt = now;
                return;
            }
            Show(message, kind, now);
        }

        void Show(string message, NoticeKind kind, float now)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            visual.gameObject.SetActive(true);
            bool good = kind == NoticeKind.Good;
            iconColor = good ? UiTheme.MoneyPlus : UiTheme.Danger;
            iconMark.gameObject.SetActive(!good);
            iconCheck.gameObject.SetActive(good);
            text.text = message;
            plate.Fit(); // 글자 크기에 판을 맞춘 뒤 아이콘을 판 왼쪽 안쪽에
            plate.DrawOnTop(); // 쥔 카드 · 물건에 가려지지 않게 (자막과 같음)
            Rect area = plate.Area;
            Vector3 iconPos = new Vector3(area.xMin + PlatePadding.x * 0.5f + 0.001f, area.center.y, 0.0005f);
            iconCircle.transform.localPosition = iconPos;
            iconMark.transform.localPosition = iconPos;

            transform.position = CardShop.Tutorial.CaptionPlacement.Front(cam.transform.position, cam.transform.forward).position;
            shownAt = now;
            hideAt = now + WorldToast.SecondsFor(message);
            ApplyLook(now);
            Sfx.Play(SoundId.TutorialDing); // 가벼운 알림 소리 (칸이 비면 조용)
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (pending != null && !CardShop.Tutorial.TutorialCaption.IsShowing)
            {
                string m = pending;
                pending = null;
                if (now - pendingAt <= StaleSeconds) Show(m, pendingKind, now); // 오래된 경고는 버림
            }
            if (visual == null || !visual.gameObject.activeSelf) return;
            if (CardShop.Tutorial.TutorialCaption.IsShowing || now >= hideAt) { visual.gameObject.SetActive(false); return; } // 자막이 뜨면 바로 비킴
            ApplyLook(now);
        }

        // 튀어 오르는 크기, 흐려짐, 머리 쪽 바라보기
        void ApplyLook(float now)
        {
            float t = Mathf.Clamp01((now - shownAt) / PopSeconds);
            float pop = t < 0.6f ? Mathf.Lerp(0.85f, 1.05f, t / 0.6f) : Mathf.Lerp(1.05f, 1f, (t - 0.6f) / 0.4f);
            visual.localScale = Vector3.one * (BaseScale * pop);

            float a = Mathf.Clamp01((hideAt - now) / FadeSeconds);
            text.alpha = a;
            plate.SetAlpha(a);
            iconMark.alpha = a;
            Color c = iconColor;
            c.a = a;
            iconCircle.color = c;
            iconCheck.color = new Color(1f, 1f, 1f, a);

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 dir = transform.position - cam.transform.position;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir, Vector3.up); // TMP는 −Z 쪽에서 읽힘
        }
    }
}
