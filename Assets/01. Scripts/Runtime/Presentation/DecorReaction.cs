using System.Collections;
using System.Collections.Generic;
using CardShop.Cards;
using CardShop.Core;
using CardShop.Logic;
using CardShop.UI;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CardShop.Presentation
{
    /// <summary>
    /// 꾸미기 가구의 생활 반응 (경제시스템_설계 14-4, DEC-040 U3). 돈과 상관없는 "살아 있는 방":
    /// - 포스터: 내일의 인기 속성(★ 1순위 · ☆ 2순위)을 벽에 붙은 글로 보여 줌 (커뮤니티를 안 봐도 앎)
    /// - 스탠드 조명: 레이 · 손으로 누르면 켜고 끔 (딸깍)
    /// - 에어컨: 누르면 켜고 끔 ("시원하다~")
    /// - 슬라임 피규어: 누르면 말랑하게 찌그러졌다 돌아옴
    /// RoomUpgradeView가 가구를 놓은 뒤 Attach로 붙입니다. 러그는 반응 없음.
    /// </summary>
    public sealed class DecorReaction : MonoBehaviour
    {
        enum Mode { Poster, Lamp, AirCon, Figure }

        const float SquishSeconds = 0.35f;

        Mode mode;
        /// <summary>어느 꾸미기인지 (튜토리얼이 가리킬 때)</summary>
        public UpgradeKind Kind { get; private set; }
        Transform interior;
        TMP_FontAsset font;
        TMP_Text posterText;
        WorldToast toast;
        bool on = true;
        Vector3 baseScale;
        string lastPoster;

        public static void Attach(UpgradeKind kind, List<GameObject> spawned, Transform interior, TMP_FontAsset font)
        {
            if (spawned == null) return;
            Mode mode;
            switch (kind)
            {
                case UpgradeKind.Poster: mode = Mode.Poster; break;
                case UpgradeKind.Lamp: mode = Mode.Lamp; break;
                case UpgradeKind.AirCon: mode = Mode.AirCon; break;
                case UpgradeKind.Figure: mode = Mode.Figure; break;
                default: return;
            }
            foreach (GameObject go in spawned)
            {
                if (go == null || go.GetComponent<DecorReaction>() != null) continue;
                var r = go.AddComponent<DecorReaction>();
                r.mode = mode;
                r.Kind = kind;
                r.interior = interior;
                r.font = font;
                r.Build();
            }
        }

        void Build()
        {
            baseScale = transform.localScale;
            if (mode == Mode.AirCon) on = false;
            if (mode == Mode.Poster) BuildPosterLabel();
            else AddPressTarget();
        }

        // 가구 경계 상자 크기의 누름 판정 (월드 축 정렬 상자를 자식으로)
        void AddPressTarget()
        {
            Bounds b = HomeRoom.Bounds(gameObject);
            var hit = new GameObject("PressTarget");
            hit.transform.SetParent(transform, true);
            hit.transform.SetPositionAndRotation(b.center, Quaternion.identity);
            Vector3 s = hit.transform.lossyScale;
            var box = hit.AddComponent<BoxCollider>();
            box.size = new Vector3(Safe(b.size.x + 0.04f, s.x), Safe(b.size.y + 0.04f, s.y), Safe(b.size.z + 0.04f, s.z));
            var interactable = hit.AddComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(_ => Press());
        }

        static float Safe(float world, float scale) => Mathf.Abs(scale) > 1e-4f ? world / Mathf.Abs(scale) : world;

        void Press()
        {
            switch (mode)
            {
                case Mode.Lamp:
                    on = !on;
                    foreach (Light l in GetComponentsInChildren<Light>(true)) l.enabled = on;
                    Sfx.Play(SoundId.LightSwitch, transform.position);
                    break;
                case Mode.AirCon:
                    on = !on;
                    Sfx.Play(SoundId.AirConSwitch, transform.position);
                    Fan(on);
                    Toast(on ? "에어컨 켬 · 시원하다~" : "에어컨 끔");
                    break;
                case Mode.Figure:
                    StopAllCoroutines();
                    StartCoroutine(Squish());
                    Sfx.Play(SoundId.FigureSquish, transform.position); // 11-21 2차: 남자 와우를 빌려 쓰던 것
                    break;
            }
            Interaction.Haptics.BothHands(0.25f, 0.05f);
        }

        const float FanVolume = 0.5f;
        AudioSource fan;

        // 에어컨이 켜져 있는 동안 바람 소리 반복 (AirConFan 칸이 비면 조용)
        void Fan(bool running)
        {
            if (fan == null) fan = Sfx.CreateLoop(SoundId.AirConFan, gameObject);
            if (fan == null) return;
            fan.volume = Sfx.LoopVolume(SoundId.AirConFan) * FanVolume;
            if (running && !fan.isPlaying) fan.Play();
            else if (!running) fan.Stop();
        }

        IEnumerator Squish()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / SquishSeconds)
            {
                float k = Mathf.Sin(t * Mathf.PI) * (1f - t) * 0.35f; // 눌렸다 출렁이며 돌아옴
                transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + k, 1f - k, 1f + k));
                yield return null;
            }
            transform.localScale = baseScale;
        }

        void Toast(string message)
        {
            if (toast == null) toast = WorldToast.Create(transform, font);
            toast.Show(message, HomeRoom.Bounds(gameObject).center + Vector3.up * 0.3f);
        }

        // ------------------------------------------------------------------ 포스터: 내일 인기 속성

        void BuildPosterLabel()
        {
            Bounds b = HomeRoom.Bounds(gameObject);
            Vector3 room = interior != null ? interior.position : b.center + Vector3.forward;
            Vector3 toRoom = room - b.center;
            toRoom.y = 0f;
            toRoom = toRoom.sqrMagnitude > 1e-4f ? toRoom.normalized : Vector3.forward;

            RectTransform canvas = WorldUiKit.CreateCanvas("PosterLabel", transform, 420f, 200f, out GameObject content);
            canvas.position = new Vector3(b.center.x, b.min.y - 0.02f - 0.1f, b.center.z) + toRoom * 0.03f; // 포스터 바로 아래 띠
            canvas.rotation = Quaternion.LookRotation(-toRoom, Vector3.up); // 캔버스 앞면(-Z)이 방 쪽
            Vector3 ls = canvas.lossyScale;
            canvas.localScale = new Vector3(canvas.localScale.x * 0.001f / Mathf.Max(1e-4f, ls.x),
                canvas.localScale.y * 0.001f / Mathf.Max(1e-4f, ls.y), canvas.localScale.z * 0.001f / Mathf.Max(1e-4f, ls.z));
            Image(content.transform);
            posterText = WorldUiKit.AddText(content.transform, "Text", "", new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), 40f, UiTheme.SoftText, font);
            posterText.alignment = TextAlignmentOptions.Center;
            posterText.enableAutoSizing = true;
            posterText.fontSizeMin = 20f;
            posterText.fontSizeMax = 44f;
        }

        static void Image(Transform parent)
        {
            var bg = WorldUiKit.AddImage(parent, "Back", Vector2.zero, Vector2.one, UiTheme.OsWindow);
            bg.raycastTarget = false;
        }

        void Update()
        {
            if (mode != Mode.Poster || posterText == null) return;
            GameContext ctx = GameContext.Instance;
            if (ctx == null || ctx.Session == null || ctx.Market == null) return;
            MarketDay m = ctx.Market.For(ctx.Session.Day + 1); // 집에서 보는 포스터 = 내일 가게 시세
            string text = !m.HasPopular ? "내일 인기 속성 없음"
                : $"내일의 인기\n<color=#FFC857>★ {CommunityBoard.ElementName(m.Primary)}</color>  <color=#FFB07A>☆ {CommunityBoard.ElementName(m.Secondary)}</color>";
            if (text == lastPoster) return;
            lastPoster = text;
            posterText.text = text;
        }
    }
}
