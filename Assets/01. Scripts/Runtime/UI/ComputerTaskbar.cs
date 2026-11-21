using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 모니터 아래 SlimeOS 작업 표시줄 (UI 개편 8번): 왼쪽 "SlimeOS" · 가운데 쪽 탭(누르면 그 쪽으로) · 오른쪽 날짜.
    /// 지금 쪽 탭은 보라, 나머지는 진한 보라 (UI 개편 샘플 8번). 쪽 넘김은 HomeComputer가 하고 이 클래스는 탭 버튼과 글자만.
    /// </summary>
    public sealed class ComputerTaskbar : MonoBehaviour
    {
        public const float HeightPx = 64f;
        const float TabW = 210f, TabGap = 10f, TabLeft = 230f;

        readonly List<Button> tabs = new List<Button>();
        TMP_FontAsset font;
        Transform row;
        TMP_Text clock;

        public static ComputerTaskbar Create(Transform parent, Vector3 localPos, Quaternion rot, float widthPx, TMP_FontAsset font)
        {
            RectTransform rt = WorldUiKit.CreateCanvas("SlimeOsTaskbar", parent, widthPx, HeightPx, out GameObject content);
            rt.localPosition = localPos;
            rt.localRotation = rot;
            Transform c = content.transform;
            Image bar = WorldUiKit.AddImage(c, "Bar", Vector2.zero, Vector2.one, UiTheme.OsTaskbar);
            bar.raycastTarget = false;
            if (Application.isPlaying)
            {
                bar.sprite = UiSprites.Rounded();
                bar.type = Image.Type.Sliced;
                bar.pixelsPerUnitMultiplier = UiSprites.Radius / 10f;
                bar.color = UiTheme.OsTaskbar;
            }

            // 왼쪽 분홍 알약 「SlimeOS」 (샘플 8번)
            Image pill = WorldUiKit.AddImage(c, "LogoPill", new Vector2(0f, 0f), new Vector2(0f, 1f), UiTheme.SoftPink);
            pill.raycastTarget = false;
            var prt = (RectTransform)pill.transform;
            prt.offsetMin = new Vector2(12f, 9f);
            prt.offsetMax = new Vector2(TabLeft - 18f, -9f);
            if (Application.isPlaying)
            {
                pill.sprite = UiSprites.Rounded();
                pill.type = Image.Type.Sliced;
                pill.pixelsPerUnitMultiplier = UiSprites.Radius / 8f;
            }
            TMP_Text logo = WorldUiKit.AddText(pill.transform, "Logo", "SlimeOS", Vector2.zero, Vector2.one, 26f, UiTheme.Ink, font);
            logo.alignment = TextAlignmentOptions.Center;

            TMP_Text clock = WorldUiKit.AddText(c, "Clock", "", new Vector2(1f, 0f), new Vector2(1f, 1f), 24f, UiTheme.SoftText, font);
            var crt = (RectTransform)clock.transform;
            crt.offsetMin = new Vector2(-300f, 0f);
            crt.offsetMax = new Vector2(-16f, 0f);
            clock.alignment = TextAlignmentOptions.MidlineRight;

            var bar2 = rt.gameObject.AddComponent<ComputerTaskbar>();
            bar2.font = font;
            bar2.row = c;
            bar2.clock = clock;
            return bar2;
        }

        public void AddTab(string name, UnityEngine.Events.UnityAction onClick)
        {
            float x = TabLeft + tabs.Count * (TabW + TabGap);
            Button b = WorldUiKit.AddButton(row, name, new Vector2(0f, 0f), new Vector2(0f, 1f), font);
            var rt = (RectTransform)b.transform;
            rt.offsetMin = new Vector2(x, 8f);
            rt.offsetMax = new Vector2(x + TabW, -8f);
            TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.fontSize = 22f; label.enableAutoSizing = true; label.fontSizeMin = 14f; label.fontSizeMax = 22f; }
            b.onClick.AddListener(onClick);
            tabs.Add(b);
            current = -1; // 새 탭도 다음 SetCurrent 때 칠함
        }

        int current = -1;
        static readonly Color Inactive = new Color(0.227f, 0.2f, 0.322f); // #3A3352 뒤 창 탭 (눈 편한 색)

        public void SetCurrent(int index)
        {
            if (index == current) return; // 넘기는 동안 매 프레임 불려도 칠하기는 바뀔 때 한 번
            current = index;
            // 지금 창 = 보라 + 흰 글자, 나머지 = 진한 보라 + 연보라 글자 (샘플 8번)
            for (int i = 0; i < tabs.Count; i++)
            {
                UiSkin.StyleButton(tabs[i], UiButtonKind.Normal);
                if (i == index) continue;
                ColorBlock cb = tabs[i].colors;
                cb.normalColor = cb.selectedColor = Inactive;
                tabs[i].colors = cb;
                TMP_Text label = tabs[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.color = UiTheme.SoftSub;
            }
        }

        public void SetClock(string text)
        {
            if (clock != null) clock.text = text;
        }
    }
}
