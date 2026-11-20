using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 집 컴퓨터 화면의 "SlimeOS" 창 모양 (UI_개편_설계 4장 8번: 레트로 OS 창 · 제목 줄 · 작업 표시줄).
    /// WorldUiKit.AddPanel로 만든 판(Background · AccentBar)을 창으로 바꿉니다: 어두운 보라 몸통 + 연보라 테 + 위 보라 제목 줄(제목 · 「_ □ ×」), 글자는 미색 (11-20 눈 편한 색).
    /// (11-20 샘플 적용: 처음 만든 어두운 진보라 창 → 샘플의 밝은 레트로 OS 창)
    /// 판에 "Title" 글자가 있으면 제목 줄로 옮기고, 없으면 title로 새로 씀. 몸통은 레이를 받아 화면 끌기(ScreenSwipeArea)가 빈 곳에서도 됨.
    /// 코드로 만든 스프라이트는 씬에 저장되지 않으므로 실행 중에만 입힙니다 (UiSkin과 같음).
    /// </summary>
    public static class SlimeOsWindow
    {
        public const float BarPx = 46f;      // 제목 줄 높이 (1px = 1mm)
        const float CornerMm = 12f;
        const string Marker = "SlimeOs_Applied";

        /// <param name="panelRoot">Background를 바로 아래에 가진 판 (판 빌더의 Content)</param>
        public static void Apply(Transform panelRoot, string title, TMP_FontAsset font)
        {
            if (panelRoot == null || panelRoot.Find(Marker) != null) return;
            if (!Application.isPlaying) return;
            new GameObject(Marker).transform.SetParent(panelRoot, false);

            Transform accent = panelRoot.Find("AccentBar");
            if (accent != null) accent.gameObject.SetActive(false);

            Image body = panelRoot.Find("Background") != null ? panelRoot.Find("Background").GetComponent<Image>() : null;
            if (body == null)
            {
                body = WorldUiKit.AddImage(panelRoot, "Background", Vector2.zero, Vector2.one, Color.white);
                body.transform.SetAsFirstSibling();
            }
            Rounded(body, UiTheme.OsWindow, CornerMm);
            body.raycastTarget = true; // 빈 곳에서 시작한 끌기도 받게
            var shadow = body.GetComponent<Shadow>();
            if (shadow == null) shadow = body.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.3f); // 아래 두께 그림자
            shadow.effectDistance = new Vector2(0f, -8f);
            int layer = body.transform.GetSiblingIndex() + 1;

            // 연보라 테 4px (몸통 위, 글자 · 버튼 아래). 잉크 가는 선은 VR에서 떨려 보였음
            Image frame = Layer(panelRoot, "OsFrame", ref layer, UiTheme.OsWindowLine, Vector2.zero, Vector2.one);
            frame.sprite = UiSprites.Frame(4);
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = UiSprites.Radius / CornerMm;

            // 제목 줄: 위에 붙은 보라 띠
            Image bar = Layer(panelRoot, "OsTitleBar", ref layer, UiTheme.OsTitleBar, new Vector2(0f, 1f), new Vector2(1f, 1f));
            var barRt = (RectTransform)bar.transform;
            barRt.offsetMin = new Vector2(3f, -BarPx);
            barRt.offsetMax = new Vector2(-3f, -3f);
            Rounded(bar, UiTheme.OsTitleBar, CornerMm - 3f);

            // 창 단추 「_ □ ✕」 (꾸밈). 기호 글자는 글꼴에 없을 수 있어 그림 둘 + ×
            Image minimize = WorldUiKit.AddImage(bar.transform, "Min", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), UiTheme.SoftText);
            minimize.raycastTarget = false;
            ((RectTransform)minimize.transform).sizeDelta = new Vector2(16f, 4f);
            ((RectTransform)minimize.transform).anchoredPosition = new Vector2(-88f, -6f);
            Image maximize = WorldUiKit.AddImage(bar.transform, "Max", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), UiTheme.SoftText);
            maximize.raycastTarget = false;
            maximize.sprite = UiSprites.Frame(6, 300f);
            maximize.type = Image.Type.Sliced;
            ((RectTransform)maximize.transform).sizeDelta = new Vector2(16f, 16f);
            ((RectTransform)maximize.transform).anchoredPosition = new Vector2(-56f, 0f);
            TMP_Text close = WorldUiKit.AddText(bar.transform, "Close", "×", new Vector2(1f, 0f), new Vector2(1f, 1f), 30f, UiTheme.SoftText, font);
            ((RectTransform)close.transform).offsetMin = new Vector2(-38f, 0f);
            ((RectTransform)close.transform).offsetMax = new Vector2(-12f, 0f);
            close.alignment = TextAlignmentOptions.Center;

            RecolorSoft(panelRoot); // 어두운 창: 밝은 글자를 눈 편한 미색 · 낮은 채도로
            close.color = UiTheme.SoftText;

            // 제목 글자: 있던 "Title"을 제목 줄로 옮김
            Transform old = panelRoot.Find("Title");
            TMP_Text label = old != null ? old.GetComponent<TMP_Text>() : null;
            if (label == null) label = WorldUiKit.AddText(bar.transform, "Title", title ?? "", Vector2.zero, Vector2.one, 26f, UiTheme.Text, font);
            else
            {
                label.transform.SetParent(bar.transform, false);
                if (!string.IsNullOrEmpty(title)) label.text = title;
            }
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(18f, 0f);
            lrt.offsetMax = new Vector2(-120f, 0f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = UiTheme.SoftText;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 26f;
            label.raycastTarget = false;
            UiFonts.Apply(label);

            foreach (Button b in panelRoot.GetComponentsInChildren<Button>(true)) UiSkin.StyleButton(b, UiButtonKind.Normal);
        }

        /// <summary>어두운 판 위 글자(버튼 글자 제외)를 눈 편한 색으로: 흰 · 크림 → 미색, 금색 → 낮은 채도 금색, 연보라 → 보조색</summary>
        public static void RecolorSoft(Transform root)
        {
            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.GetComponentInParent<Button>(true) != null) continue;
                Color c = t.color, to;
                if (Near(c, UiTheme.Gold)) to = UiTheme.SoftGold;
                else if (Near(c, UiTheme.Text) || Near(c, Color.white) || Near(c, UiTheme.Ink)) to = UiTheme.SoftText;
                else if (Near(c, UiTheme.SubText)) to = UiTheme.SoftSub;
                else continue;
                to.a = c.a;
                t.color = to;
            }
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.06f;

        static void Rounded(Image image, Color color, float cornerMm)
        {
            image.sprite = UiSprites.Rounded();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = UiSprites.Radius / Mathf.Max(1f, cornerMm);
            image.color = color;
        }

        static Image Layer(Transform parent, string name, ref int siblingIndex, Color color, Vector2 min, Vector2 max)
        {
            Image image = WorldUiKit.AddImage(parent, name, min, max, color);
            image.raycastTarget = false;
            image.transform.SetSiblingIndex(siblingIndex++);
            return image;
        }
    }
}
