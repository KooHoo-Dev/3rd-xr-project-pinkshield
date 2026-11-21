using CardShop.Cards;
using CardShop.Logic;
using CardShop.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.Presentation
{
    /// <summary>
    /// 바인더 판을 "진짜 카드 바인더"처럼 (UI 개편 샘플 6-2, 11-20 적용안): 남색 표지 + 왼쪽 금속 링 + 밝은 속지,
    /// 속성 탭은 오른쪽으로 튀어나온 색인 탭(속성 색), 맨 아래 「정렬」. 칸 안의 실제 카드 · 점선 「?」는 BinderSlotCard가 그림.
    /// 씬은 그대로 두고 실행할 때 한 번 입힘 (HomeUpgradeInstaller가 부름). 칸 버튼은 건드리지 않음.
    /// </summary>
    public static class BinderSkin
    {
        const string PageName = "BinderPage";
        const string Marker = "BinderSkin_Applied";
        const float CornerMm = 16f;
        const float RingMm = 20f;
        static readonly float[] RingY = { 0.2f, 0.5f, 0.8f };
        // 속지 여백 (mm): 왼쪽은 링 자리
        const float SheetLeft = 26f, SheetOther = 10f;
        // 오른쪽 색인 탭 (판 폭 비율 · 높이 비율)
        const float TabOut = 0.13f, TabTop = 0.93f, TabH = 0.072f, TabGap = 0.01f;

        // 11-20 눈 편한 색: 밝은 속지가 VR에서 눈부셔 → 남색 표지 + 짙은 남색 속지, 카드가 가장 밝게 보이도록
        static readonly Color Navy = UiTheme.BinderCover;
        static readonly Color Sheet = UiTheme.BinderPage;

        public static void Apply(BinderView view)
        {
            if (view == null || !Application.isPlaying) return;
            Transform page = FindChild(view.transform, PageName);
            if (page == null || page.Find(Marker) != null) return;
            new GameObject(Marker).transform.SetParent(page, false);

            Transform accent = page.Find("AccentBar");
            if (accent != null) accent.gameObject.SetActive(false);
            Image bg = page.Find("Background") != null ? page.Find("Background").GetComponent<Image>() : null;
            int layer = 0;
            if (bg != null)
            {
                Round(bg, Navy, CornerMm);
                bg.raycastTarget = false;
                layer = bg.transform.GetSiblingIndex() + 1;
            }

            // 속지 (표지 안쪽)
            Image sheet = Layer(page, "Sheet", ref layer, Sheet, Vector2.zero, Vector2.one);
            var srt = (RectTransform)sheet.transform;
            srt.offsetMin = new Vector2(SheetLeft, SheetOther);
            srt.offsetMax = new Vector2(-SheetOther, -SheetOther);
            Round(sheet, Sheet, 8f);

            // 왼쪽 여백의 금속 링
            foreach (float y in RingY)
            {
                Image ring = Layer(page, "Ring", ref layer, UiTheme.Metal, new Vector2(0f, y), new Vector2(0f, y));
                ring.sprite = UiSprites.Circle();
                var rt = (RectTransform)ring.transform;
                rt.sizeDelta = new Vector2(RingMm, RingMm);
                rt.anchoredPosition = new Vector2(SheetLeft * 0.5f, 0f);
            }

            // 쪽 정보 · 안내 글자 → 미색 (칸 안 · 버튼 글자는 제외)
            foreach (TMPro.TMP_Text t in page.GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (t.GetComponentInParent<BinderSlotView>(true) == null && t.GetComponentInParent<Button>(true) == null)
                    t.color = UiTheme.SoftText;
            PlaceIndexTabs(page, view.Styles);
            Button prev = FindButton(page, "Prev"), next = FindButton(page, "Next");
            if (prev != null) UiSkin.StyleButton(prev, UiButtonKind.Normal);
            if (next != null) UiSkin.StyleButton(next, UiButtonKind.Normal);
        }

        // 속성 탭(Tab_All …) + 정렬(Tab_Sort)을 판 오른쪽 바깥에 세로로, 속성 색으로
        static void PlaceIndexTabs(Transform page, CardRarityStyles styles)
        {
            float y = TabTop;
            for (int i = 0; i <= BinderTabs.Order.Length; i++)
            {
                bool sort = i == BinderTabs.Order.Length;
                Transform t = page.Find(sort ? "Tab_Sort" : "Tab_" + BinderTabs.Order[i]);
                if (t == null || !t.TryGetComponent(out Button b)) continue;
                if (sort) y -= TabGap * 2f; // 정렬은 조금 떨어뜨림
                var rt = (RectTransform)t;
                rt.anchorMin = new Vector2(1f - 0.01f, y - TabH);
                rt.anchorMax = new Vector2(1f + TabOut, y);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                y -= TabH + TabGap;

                CardElement e = sort ? CardElement.None : BinderTabs.ToElement(BinderTabs.Order[i]);
                // 속성 색을 조금 흐리고(채도 ↓) 어둡게 — 어두운 표지 옆에서 눈부시지 않게
                Color c = e == CardElement.None || styles == null ? UiTheme.SoftPaper
                    : Color.Lerp(Color.Lerp(styles.GetElementColor(e), Color.white, 0.2f), new Color(0.6f, 0.6f, 0.65f), 0.2f);
                UiSkin.StyleButton(b, UiButtonKind.Paper);
                ColorBlock cb = b.colors;
                cb.normalColor = c;
                cb.selectedColor = c;
                cb.highlightedColor = Color.Lerp(c, Color.white, 0.35f);
                cb.pressedColor = Color.Lerp(c, Color.black, 0.15f);
                b.colors = cb;
            }
        }

        static Button FindButton(Transform page, string word)
        {
            foreach (Button b in page.GetComponentsInChildren<Button>(true))
                if (b.name.Contains(word) || b.name.Contains(word == "Prev" ? "이전" : "다음")) return b;
            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static void Round(Image image, Color color, float cornerMm)
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
