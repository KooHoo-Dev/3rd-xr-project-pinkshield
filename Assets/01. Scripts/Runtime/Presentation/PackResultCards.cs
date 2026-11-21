using System.Collections;
using System.Collections.Generic;
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
    /// "이번 팩 결과" 카드판 (UI 개편 샘플 ⑤, 11-22 사용자 선택): 톱로더 같은 진보라 판 + 금 테 + 잉크 바깥 테,
    /// 위 제목 · (특수팩) 분홍 알약, 가운데 실제 카드 5장(<see cref="PackResultSlot"/>), 아래 요약 띠 "새 카드 N장 · 도감 a / b · 매입가 $N".
    /// 유니크 이상이 나오면 판 전체에 빛줄기가 천천히 지나감 (Pocket의 등급별 연출 크기 차이).
    ///
    /// 눈 편한 색 (VR, vr-comfort 기준) — 샘플(모니터용)에서 바꾼 것:
    /// 금 테 #FFD159 → SoftGold, 밝은 초록 → SoftGreen, 글자 크림 → 미색 SoftText, 판 바깥 금빛 40px 35% → 알파 0.12(유니크 이상 0.25),
    /// 빛줄기 흰색 8% 그대로 · 5초에 한 번 (깜빡임 없이 천천히). 큰 밝은 면 없음 (판 = 휘도 약 0.03의 진보라).
    /// 원점 = 판 아래 가운데 (더미 위에 서도록), 앞면 -Z. 크기 단위 m.
    /// </summary>
    public sealed class PackResultCards : MonoBehaviour
    {
        const float Pad = 0.016f;
        const float CardH = 0.074f;
        const float Gap = 0.01f;
        const float Corner = 0.014f;
        const float FrameM = 0.004f;
        const float InkEdgeM = 0.005f;
        const float StripH = 0.028f;
        const float HintH = 0.014f;
        const float HeaderH = 0.032f;
        const float MinWidth = 0.26f;
        const float ShinePeriod = 5f;   // 빛줄기 한 번 + 쉬는 시간 (초)
        const float ShineMove = 1.6f;   // 판을 가로지르는 시간 (초)
        const float ShineAlpha = 0.08f;
        const float InnerLineInset = 0.004f; // 금 테 안쪽 흰 선까지
        const float CrestW = 0.026f;         // 위 가운데 금 돔 폭

        static float CardW => CardH * (CardView.Width / CardView.Height);

        readonly List<PackResultSlot> slots = new List<PackResultSlot>();
        SpriteRenderer outerGlow, inkEdge, fill, frame, strip, pill, shine;
        SpriteRenderer pattern, innerLine, crest, crestEdge, titleSpark; // 슬라임 카드 문양 (뒷면 A안 · 앞면 마크)
        TextMeshPro title, pillText, summary, value, hint;
        Coroutine slotPop;
        float width, height;
        bool shining;
        float shineStart;

        public static PackResultCards Create(Transform parent)
        {
            var go = new GameObject("PackResultCards");
            go.transform.SetParent(parent, false);
            var board = go.AddComponent<PackResultCards>();
            board.Build();
            go.SetActive(false);
            return board;
        }

        void Build()
        {
            float ppu = UiSprites.Radius / Corner;
            outerGlow = MakeSprite(transform, "OuterGlow", UiSprites.Glow(UiSprites.Radius / 0.02f), Color.clear, -2, Vector2.one, Vector3.zero);
            inkEdge = MakeSprite(transform, "InkEdge", UiSprites.Rounded(UiSprites.Radius / (Corner + InkEdgeM)), UiTheme.ResultStrip, -1, Vector2.one, Vector3.zero);
            fill = MakeSprite(transform, "Fill", UiSprites.Rounded(ppu), UiTheme.CardBack, 0, Vector2.one, Vector3.zero);
            // 카드 뒷면 A안 문양: 옅은 슬라임 돔 무늬(흰 5%, 한 칸 30mm) + 금 테 안쪽 흰 선 + 위 가운데 금 돔
            pattern = MakeSprite(transform, "DomePattern", UiSprites.DomePattern(60f / 0.03f), new Color(1f, 1f, 1f, 0.05f), 1, Vector2.one, Vector3.zero);
            pattern.drawMode = SpriteDrawMode.Tiled;
            float lineCorner = Corner - InnerLineInset;
            innerLine = MakeSprite(transform, "InnerLine", UiSprites.Frame(Mathf.Max(1, Mathf.RoundToInt(0.0012f * UiSprites.Radius / lineCorner)), UiSprites.Radius / lineCorner),
                new Color(1f, 1f, 1f, 0.16f), 1, Vector2.one, Vector3.zero);
            frame = MakeSprite(transform, "Frame", UiSprites.Frame(Mathf.RoundToInt(FrameM * ppu), ppu), UiTheme.SoftGold, 2, Vector2.one, Vector3.zero);
            crestEdge = MakeDome(transform, "CrestEdge", CrestW * 1.14f, UiTheme.ResultStrip, 2);
            crest = MakeDome(transform, "Crest", CrestW, UiTheme.SoftGold, 3);
            var crestShine = MakeSprite(crest.transform, "Shine", UiSprites.Circle(), new Color(1f, 1f, 1f, 0.45f), 4, Vector2.one, new Vector3(-0.105f, 0.05f, 0f));
            crestShine.drawMode = SpriteDrawMode.Simple;
            crestShine.transform.localScale = new Vector3(0.15f, 0.07f, 1f);
            shine = MakeSprite(transform, "Shine", UiSprites.SoftBand(), Color.clear, 3, Vector2.one, Vector3.zero);
            strip = MakeSprite(transform, "Strip", UiSprites.Rounded(UiSprites.Radius / 0.01f), UiTheme.ResultStrip, 3, Vector2.one, Vector3.zero);
            pill = MakeSprite(transform, "Pill", UiSprites.Rounded(UiSprites.Radius / 0.009f), UiTheme.SoftPink, 3, new Vector2(0.09f, 0.02f), Vector3.zero);

            title = MakeText(transform, "Title", 0.22f, UiTheme.SoftGold, 4, new Vector2(0.2f, HeaderH));
            title.alignment = TextAlignmentOptions.Left;
            title.text = "이번 팩 결과";
            // 유니크 이상: 제목 옆 네 갈래 반짝임 (카드 등급 마크 옆 반짝임과 같은 모양)
            titleSpark = MakeSprite(transform, "TitleSpark", UiSprites.Sparkle(), UiTheme.SoftGold, 4, Vector2.one, Vector3.zero);
            titleSpark.drawMode = SpriteDrawMode.Simple;
            titleSpark.transform.localScale = Vector3.one * (0.015f / 0.64f);
            pillText = MakeText(pill.transform, "Label", 0.12f, UiTheme.Ink, 4, new Vector2(0.09f, 0.02f));
            summary = MakeText(transform, "Summary", 0.13f, UiTheme.SoftText, 4, new Vector2(0.24f, StripH));
            summary.alignment = TextAlignmentOptions.Left;
            value = MakeText(transform, "Value", 0.13f, UiTheme.SoftText, 4, new Vector2(0.12f, StripH));
            value.alignment = TextAlignmentOptions.Right;
            hint = MakeText(transform, "Hint", 0.09f, UiTheme.SoftSub, 4, new Vector2(0.3f, HintH));
            hint.text = "카드는 퇴근할 때 보관함으로 옮겨져요";
        }

        public void Show(PackResult pack, CardRarityStyles styles)
        {
            int n = pack.Cards.Length;
            while (slots.Count < n) slots.Add(PackResultSlot.Create(transform, CardW, CardH, 5));
            Layout(n);

            int newCount = 0;
            float buyback = 0f;
            GameContext ctx = GameContext.Instance;
            for (int i = 0; i < slots.Count; i++)
            {
                bool used = i < n;
                slots[i].gameObject.SetActive(used);
                if (!used) continue;
                CardData c = pack.Cards[i];
                bool isNew = pack.IsNew != null && i < pack.IsNew.Length && pack.IsNew[i];
                if (isNew) newCount++;
                if (c != null && ctx != null && ctx.Rarity != null) buyback += ctx.Rarity.BuybackPrice(c.rarity);
                slots[i].Show(c, isNew, styles);
            }

            string gold = ColorUtility.ToHtmlStringRGB(UiTheme.SoftGold);
            int discovered = ctx != null && ctx.Session != null ? ctx.Session.Collection.DiscoveredCount : 0;
            int total = ctx != null && ctx.Cards != null ? ctx.Cards.Count : 0;
            summary.text = RevealText.ResultSummary(newCount, discovered, total, ColorUtility.ToHtmlStringRGB(UiTheme.SoftGreen), gold);
            value.text = RevealText.ResultValue(buyback, gold);

            bool special = pack.Special != SpecialPackType.None;
            pill.gameObject.SetActive(special);
            if (special) pillText.text = RevealText.SpecialLabel(pack.Special);

            bool big = pack.BestRarity >= CardRarity.Unique;
            Color g = UiTheme.SoftGold;
            g.a = big ? 0.25f : 0.12f;
            outerGlow.color = g;
            shining = big;
            shineStart = Time.time;
            shine.enabled = false;
            titleSpark.gameObject.SetActive(big);

            gameObject.SetActive(true);
            if (slotPop != null) StopCoroutine(slotPop);
            slotPop = StartCoroutine(PopSlots(n));
        }

        void Layout(int n)
        {
            float gridW = n * CardW + Mathf.Max(0, n - 1) * Gap;
            width = Mathf.Max(gridW, MinWidth) + Pad * 2f;

            float hintY = Pad + HintH * 0.5f;
            float stripY = Pad + HintH + 0.006f + StripH * 0.5f;
            float slotEdge = 0.0055f; // 등급 테가 카드 밖으로 나온 만큼
            float cardY = stripY + StripH * 0.5f + 0.008f + PackResultSlot.ChipHeightM + 0.004f + slotEdge + CardH * 0.5f;
            float headerY = cardY + CardH * 0.5f + slotEdge + 0.012f + HeaderH * 0.5f; // NEW 표가 위로 7mm 나옴
            height = headerY + HeaderH * 0.5f + Pad * 0.8f;

            Vector3 mid = new Vector3(0f, height * 0.5f, 0f);
            Vector2 size = new Vector2(width, height);
            Place(fill, mid, size);
            Place(frame, mid, size);
            Place(inkEdge, mid + new Vector3(0f, 0f, 0.0005f), size + Vector2.one * InkEdgeM * 2f);
            Place(outerGlow, mid + new Vector3(0f, 0f, 0.001f), size + Vector2.one * (InkEdgeM + 0.02f) * 2f);
            shine.size = new Vector2(0.07f, height - Corner * 2f);
            Place(pattern, mid, size - Vector2.one * FrameM * 2f);
            Place(innerLine, mid, size - Vector2.one * InnerLineInset * 2f);
            // 금 돔: 아래 1/3이 윗테에 걸치게 (그림 아래 2px 여백 · 돔 높이 = 폭 × 3/4)
            Vector3 crestPos = new Vector3(0f, height + CrestW * 0.75f * 0.5f - CrestW * 0.75f * 0.35f, -0.0005f);
            crest.transform.localPosition = crestPos;
            crestEdge.transform.localPosition = crestPos + new Vector3(0f, -CrestW * 0.75f * 0.06f, 0.0003f);

            float inner = width * 0.5f - Pad;
            Place(strip, new Vector3(0f, stripY, 0f), new Vector2(width - Pad * 2f, StripH));
            summary.rectTransform.sizeDelta = new Vector2((width - Pad * 2f) * 0.68f, StripH);
            summary.transform.localPosition = new Vector3(-inner + 0.01f + summary.rectTransform.sizeDelta.x * 0.5f, stripY, 0f);
            value.rectTransform.sizeDelta = new Vector2((width - Pad * 2f) * 0.3f, StripH);
            value.transform.localPosition = new Vector3(inner - 0.01f - value.rectTransform.sizeDelta.x * 0.5f, stripY, 0f);
            hint.transform.localPosition = new Vector3(0f, hintY, 0f);

            title.transform.localPosition = new Vector3(-inner + title.rectTransform.sizeDelta.x * 0.5f, headerY, 0f);
            titleSpark.transform.localPosition = new Vector3(-inner + title.preferredWidth + 0.006f + 0.0075f, headerY + 0.004f, 0f);
            pill.transform.localPosition = new Vector3(inner - pill.size.x * 0.5f, headerY, 0f);

            float x0 = -gridW * 0.5f + CardW * 0.5f;
            for (int i = 0; i < n && i < slots.Count; i++)
                slots[i].transform.localPosition = new Vector3(x0 + i * (CardW + Gap), cardY, 0f);
        }

        // 판이 뜬 뒤 왼쪽 칸부터 차례로 "톡" (Balatro처럼 하나씩 보이게)
        IEnumerator PopSlots(int n)
        {
            for (int i = 0; i < n; i++) slots[i].SetPop(0f);
            yield return new WaitForSeconds(0.12f);
            const float each = 0.18f, stagger = 0.07f;
            float t0 = Time.time;
            while (true)
            {
                float t = Time.time - t0;
                bool done = true;
                for (int i = 0; i < n; i++)
                {
                    float u = Mathf.Clamp01((t - i * stagger) / each);
                    if (u < 1f) done = false;
                    slots[i].SetPop(Mathf.SmoothStep(0f, 1f, u) + Mathf.Sin(u * Mathf.PI) * 0.1f);
                }
                if (done) break;
                yield return null;
            }
            slotPop = null;
        }

        void Update()
        {
            if (!shining) return;
            float t = (Time.time - shineStart) % ShinePeriod;
            bool on = t < ShineMove;
            shine.enabled = on;
            if (!on) return;
            float u = t / ShineMove;
            float travel = width * 0.5f - Corner - shine.size.x * 0.5f;
            shine.transform.localPosition = new Vector3(Mathf.Lerp(-travel, travel, u), height * 0.5f, 0f);
            shine.color = new Color(1f, 1f, 1f, ShineAlpha * Mathf.Sin(u * Mathf.PI)); // 양 끝에서 사라져 판 밖으로 안 나감
        }

        public void Hide()
        {
            if (slotPop != null) { StopCoroutine(slotPop); slotPop = null; }
            shining = false;
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ 만들기 도우미 (PackResultSlot도 씀)

        static void Place(SpriteRenderer sr, Vector3 localCenter, Vector2 size)
        {
            sr.transform.localPosition = localCenter;
            sr.size = size;
        }

        /// <summary>Sliced 스프라이트 하나 (크기는 m). 순서가 클수록 앞</summary>
        internal static SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, Color color, int order, Vector2 size, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = color;
            sr.sortingOrder = order;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            return sr;
        }

        /// <summary>슬라임 돔 하나 (폭 widthM, 그림 64 × 48픽셀 · ppu 100)</summary>
        static SpriteRenderer MakeDome(Transform parent, string name, float widthM, Color color, int order)
        {
            SpriteRenderer sr = MakeSprite(parent, name, UiSprites.Dome(), color, order, Vector2.one, Vector3.zero);
            sr.drawMode = SpriteDrawMode.Simple;
            sr.transform.localScale = Vector3.one * (widthM / 0.64f);
            return sr;
        }

        /// <summary>3D 글자 하나 (가운데 정렬 · 줄바꿈 없음 · 글꼴 하나 굵게)</summary>
        internal static TextMeshPro MakeText(Transform parent, string name, float fontSize, Color color, int order, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            UiFonts.Apply(t);
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.rectTransform.sizeDelta = size;
            var mr = t.GetComponent<MeshRenderer>();
            mr.sortingOrder = order;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return t;
        }
    }
}
