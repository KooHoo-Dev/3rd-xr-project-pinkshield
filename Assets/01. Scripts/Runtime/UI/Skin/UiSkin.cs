using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>판 재질 (UI_개편_설계 C안: 일 = 종이, 보상 = 카드, 타이틀 = 입간판)</summary>
    public enum UiSurface
    {
        /// <summary>영수증: 흰 종이 + 아래 톱니 (정산)</summary>
        Receipt,
        /// <summary>종이 (상장 · 메모)</summary>
        Paper,
        /// <summary>나무 클립보드 + 종이 + 금속 집게 (일시정지)</summary>
        Clipboard,
        /// <summary>나무 틀 + 칠판 (타이틀 입간판)</summary>
        Chalkboard,
        /// <summary>진보라 카드 + 금박 테두리 (보상)</summary>
        Card,
    }

    public enum UiButtonKind
    {
        /// <summary>화면에서 가장 중요한 버튼 하나 (분홍)</summary>
        Primary,
        /// <summary>보통 버튼 (보라)</summary>
        Normal,
        /// <summary>종이 버튼 (작은 설정 · 토글)</summary>
        Paper,
        /// <summary>되돌릴 수 없는 버튼 (종이 + 빨간 글자)</summary>
        Danger,
    }

    /// <summary>
    /// 이미 씬에 있는 단색 판(WorldUiKit.AddPanel로 만든 Background · AccentBar)에 재질을 입힙니다.
    /// 11-20부터 Shop.unity가 원본이라 씬을 다시 만들지 않고, 각 화면 스크립트가 Awake에서 한 번 부릅니다.
    /// 밝은 재질(종이 · 영수증 · 클립보드)이면 크림색 글자를 잉크색으로 바꾸고, 판 안의 버튼은 모두 Normal 모양으로 칠합니다
    /// (특별한 버튼은 부른 쪽이 <see cref="StyleButton"/>으로 다시 칠함).
    /// </summary>
    public static class UiSkin
    {
        const float PanelCornerMm = 14f;
        const float ButtonCornerMm = 12f;
        const string Marker = "Skin_Applied";

        static readonly Color PaperHover = new Color(1f, 0.86f, 0.92f);
        static readonly Color PaperPressed = new Color(0.92f, 0.78f, 0.85f);
        static readonly Color DangerHover = new Color(1f, 0.84f, 0.84f);

        /// <param name="panelRoot">Background · AccentBar를 바로 아래에 가진 오브젝트 (판 빌더의 Content, 일시정지는 캔버스)</param>
        public static void ApplyPanel(Transform panelRoot, UiSurface surface)
        {
            if (panelRoot == null || panelRoot.Find(Marker) != null) return; // 두 번 칠하지 않음
            if (!Application.isPlaying) return; // 코드로 만든 스프라이트는 씬에 저장되지 않음 → 실행할 때만
            new GameObject(Marker).transform.SetParent(panelRoot, false);

            Transform accent = panelRoot.Find("AccentBar");
            if (accent != null) accent.gameObject.SetActive(false);

            Image bg = panelRoot.Find("Background") != null ? panelRoot.Find("Background").GetComponent<Image>() : null;
            if (bg == null) bg = WorldUiKit.AddImage(panelRoot, "Background", Vector2.zero, Vector2.one, Color.white);
            bg.raycastTarget = false;
            int layer = bg.transform.GetSiblingIndex() + 1; // 재질 조각은 바탕 바로 위, 글자 · 버튼 아래

            switch (surface)
            {
                case UiSurface.Receipt:
                    Round(bg, UiTheme.Receipt, 4f);
                    AddDropShadow(bg);
                    Image tear = Layer(panelRoot, "TearEdge", ref layer, UiTheme.Receipt, new Vector2(0f, 0f), new Vector2(1f, 0f));
                    ((RectTransform)tear.transform).offsetMin = new Vector2(0f, -8f);
                    tear.sprite = UiSprites.TearEdge();
                    tear.type = Image.Type.Tiled;
                    break;
                case UiSurface.Paper:
                    Round(bg, UiTheme.Paper, 6f);
                    AddDropShadow(bg);
                    break;
                case UiSurface.Clipboard:
                    Round(bg, UiTheme.Kraft, 18f);
                    AddDropShadow(bg);
                    Image sheet = Layer(panelRoot, "Sheet", ref layer, UiTheme.Paper, Vector2.zero, Vector2.one);
                    Inset(sheet, 20f, 20f, 20f, 34f);
                    Round(sheet, UiTheme.Paper, 6f);
                    Image clip = Layer(panelRoot, "Clip", ref layer, UiTheme.Metal, new Vector2(0.33f, 1f), new Vector2(0.67f, 1f));
                    ((RectTransform)clip.transform).offsetMin = new Vector2(0f, -22f);
                    ((RectTransform)clip.transform).offsetMax = new Vector2(0f, 12f);
                    Round(clip, UiTheme.Metal, 8f);
                    break;
                case UiSurface.Chalkboard:
                    Round(bg, UiTheme.Wood, 22f);
                    AddDropShadow(bg);
                    Image board = Layer(panelRoot, "Chalk", ref layer, UiTheme.Chalkboard, Vector2.zero, Vector2.one);
                    Inset(board, 22f, 22f, 22f, 22f);
                    Round(board, UiTheme.Chalkboard, 10f);
                    break;
                case UiSurface.Card:
                    Round(bg, UiTheme.CardBack, 18f);
                    Image frame = Layer(panelRoot, "GoldFrame", ref layer, UiTheme.Gold, Vector2.zero, Vector2.one);
                    frame.sprite = UiSprites.Frame(5);
                    frame.type = Image.Type.Sliced;
                    frame.pixelsPerUnitMultiplier = UiSprites.Radius / 18f;
                    break;
            }

            if (IsLight(surface)) RecolorForPaper(panelRoot);
            foreach (Button b in panelRoot.GetComponentsInChildren<Button>(true)) StyleButton(b, UiButtonKind.Normal);
        }

        /// <summary>둥근 모서리 + 아래 두께(그림자) + 종류별 색. 판 빌더 · 런타임 화면 · 복제 버튼이 모두 이것을 씀</summary>
        public static void StyleButton(Button button, UiButtonKind kind)
        {
            if (button == null) return;
            // 코드로 만든 스프라이트는 씬에 저장되지 않으므로(DontSave) 에디터 빌더에서는 색만 칠하고, 모양은 실행할 때 입힘
            if (Application.isPlaying && button.targetGraphic is Image image)
            {
                image.sprite = UiSprites.Rounded();
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = UiSprites.Radius / ButtonCornerMm;
                image.color = Color.white;
                var depth = image.GetComponent<Shadow>();
                if (depth == null) depth = image.gameObject.AddComponent<Shadow>();
                depth.effectColor = UiTheme.ButtonDepth;
                depth.effectDistance = new Vector2(0f, -7f);
            }

            Color normal, hover, pressed, label;
            switch (kind)
            {
                case UiButtonKind.Primary: normal = UiTheme.ButtonPrimary; hover = UiTheme.ButtonPrimaryHover; pressed = UiTheme.ButtonPressed; label = UiTheme.Text; break;
                case UiButtonKind.Paper: normal = UiTheme.Paper; hover = PaperHover; pressed = PaperPressed; label = UiTheme.Ink; break;
                case UiButtonKind.Danger: normal = UiTheme.Paper; hover = DangerHover; pressed = PaperPressed; label = UiTheme.Danger; break;
                default: normal = UiTheme.ButtonNormal; hover = UiTheme.ButtonHover; pressed = UiTheme.ButtonPressed; label = UiTheme.Text; break;
            }
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = normal,
                highlightedColor = hover,
                pressedColor = pressed,
                selectedColor = normal,
                disabledColor = UiTheme.ButtonDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.05f,
            };
            foreach (TMP_Text t in button.GetComponentsInChildren<TMP_Text>(true))
            {
                t.color = label;
                UiFonts.Apply(t);
            }
        }

        public static bool IsLight(UiSurface s) => s == UiSurface.Receipt || s == UiSurface.Paper || s == UiSurface.Clipboard;

        /// <summary>판 위 글자(버튼 글자 제외) 색을 종이용으로: 금색 · 크림 · 흰색 → 잉크, 연보라 → 옅은 잉크</summary>
        public static void RecolorForPaper(Transform root)
        {
            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.GetComponentInParent<Button>(true) != null) continue;
                t.color = PaperColorFor(t.color);
            }
        }

        /// <summary>어두운 판에서 쓰던 밝은 초록 강조 (NEW · 새 카드 · +돈)</summary>
        public static readonly Color BrightGreen = new Color(0.486f, 0.949f, 0.604f);

        /// <summary>
        /// 어두운 판용 글자 색 → 종이용 글자 색 (알파는 유지). 정해진 테마 색이 아니면 그대로.
        /// 색 강조는 지키되(11-20 결정) 종이 위에서 흐린 밝은 색은 같은 계열의 진한 색으로: 밝은 초록 → 진한 초록, 분홍 → 진한 분홍
        /// </summary>
        public static Color PaperColorFor(Color c)
        {
            Color to;
            if (Near(c, UiTheme.Gold) || Near(c, UiTheme.Text) || Near(c, Color.white)) to = UiTheme.Ink;
            else if (Near(c, UiTheme.SubText)) to = UiTheme.InkSoft;
            else if (Near(c, BrightGreen)) to = UiTheme.MoneyPlus;
            else if (Near(c, UiTheme.Accent)) to = UiTheme.Stamp;
            else return c;
            to.a = c.a;
            return to;
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.06f;

        static void Round(Image image, Color color, float cornerMm)
        {
            image.sprite = UiSprites.Rounded();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = UiSprites.Radius / Mathf.Max(1f, cornerMm);
            image.color = color;
        }

        static void AddDropShadow(Image image)
        {
            var s = image.GetComponent<Shadow>();
            if (s == null) s = image.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.35f);
            s.effectDistance = new Vector2(0f, -10f);
        }

        static Image Layer(Transform parent, string name, ref int siblingIndex, Color color, Vector2 min, Vector2 max)
        {
            Image image = WorldUiKit.AddImage(parent, name, min, max, color);
            image.raycastTarget = false;
            image.transform.SetSiblingIndex(siblingIndex++);
            return image;
        }

        static void Inset(Image image, float left, float right, float bottom, float top)
        {
            var rt = (RectTransform)image.transform;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
