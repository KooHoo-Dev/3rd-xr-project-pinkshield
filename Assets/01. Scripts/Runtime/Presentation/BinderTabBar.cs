using System;
using CardShop.Cards;
using CardShop.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.Presentation
{
    /// <summary>
    /// 바인더 속성 탭 버튼 줄: 누르면 탭 바꾸기 알림, 고른 탭은 노란 굵은 글씨 + 밑줄, 인기 속성 탭에는 ★ (가게 = 오늘, 집 = 내일 — BinderView가 정함).
    /// BinderView에서 떼어 냄 (큰_클래스_나누기_제안 3장). 버튼 순서는 BinderTabs.Order.
    /// </summary>
    public sealed class BinderTabBar
    {
        const float SortWidth = 0.15f; // 탭 줄 끝 정렬 버튼 폭 (줄 전체 대비)

        readonly Button[] buttons;
        readonly Color selectedText;
        readonly Button sortButton;

        /// <param name="tabButtons">BinderTabs.Order 순서의 버튼 (null이면 탭 없음)</param>
        /// <param name="sortPicked">정렬 버튼을 눌렀을 때 (null이면 정렬 버튼 없음, F1)</param>
        public BinderTabBar(Button[] tabButtons, Color selectedTextColor, Action<BinderTab> picked, Action sortPicked = null)
        {
            buttons = tabButtons;
            selectedText = selectedTextColor;
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length && i < BinderTabs.Order.Length; i++)
            {
                if (buttons[i] == null) continue;
                BinderTab t = BinderTabs.Order[i];
                buttons[i].onClick.AddListener(() => picked(t));
            }
            if (sortPicked != null) sortButton = CreateSortButton(sortPicked);
        }

        // 씬을 다시 빌드하지 않도록: 탭들을 왼쪽으로 좁히고, 마지막 탭을 복제해 줄 오른쪽 끝에 [도감순 ⇄ 레어도순]
        Button CreateSortButton(Action sortPicked)
        {
            if (buttons.Length == 0 || buttons[0] == null || buttons[buttons.Length - 1] == null) return null;
            var first = (RectTransform)buttons[0].transform;
            var last = (RectTransform)buttons[buttons.Length - 1].transform;
            float left = first.anchorMin.x, right = last.anchorMax.x;
            float gap = buttons.Length > 1 ? ((RectTransform)buttons[1].transform).anchorMin.x - first.anchorMax.x : 0.012f;
            float sortLeft = right - (right - left) * SortWidth;

            // 탭 n개를 [left, sortLeft - gap] 안에 같은 폭으로
            float span = sortLeft - gap - left;
            float w = (span + gap) / buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                var rt = (RectTransform)buttons[i].transform;
                rt.anchorMin = new Vector2(left + i * w, rt.anchorMin.y);
                rt.anchorMax = new Vector2(left + (i + 1) * w - gap, rt.anchorMax.y);
            }

            Button b = UnityEngine.Object.Instantiate(buttons[buttons.Length - 1], last.parent);
            b.name = "Tab_Sort";
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => sortPicked());
            var srt = (RectTransform)b.transform;
            srt.anchorMin = new Vector2(sortLeft, srt.anchorMin.y);
            srt.anchorMax = new Vector2(right, srt.anchorMax.y);
            return b;
        }

        /// <param name="popular">★ 1순위 인기 속성</param>
        /// <param name="second">☆ 2순위 인기 속성 (튜토리얼_설계 T8)</param>
        public void Redraw(BinderTab current, CardElement popular, BinderSortOrder sort = BinderSortOrder.Dex, CardElement second = CardElement.None)
        {
            if (sortButton != null)
            {
                TMP_Text s = sortButton.GetComponentInChildren<TMP_Text>(true);
                if (s != null)
                {
                    s.text = BinderSort.Label(sort); // 기호(⇅)는 글꼴에 없을 수 있어 글자만
                    s.color = sort == BinderSortOrder.Rarity ? selectedText : Color.white;
                    s.fontStyle = FontStyles.Bold;
                }
            }
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length && i < BinderTabs.Order.Length; i++)
            {
                if (buttons[i] == null) continue;
                TMP_Text label = buttons[i].GetComponentInChildren<TMP_Text>(true);
                if (label == null) continue;
                BinderTab t = BinderTabs.Order[i];
                bool on = t == current;
                CardElement e = BinderTabs.ToElement(t);
                bool hot = popular != CardElement.None && e == popular;
                bool warm = !hot && second != CardElement.None && e == second;
                label.text = hot ? $"★{BinderTabs.Label(t)}" : warm ? $"☆{BinderTabs.Label(t)}" : BinderTabs.Label(t);
                label.color = on ? selectedText : Color.white;
                label.fontStyle = on ? FontStyles.Bold | FontStyles.Underline : FontStyles.Normal;
            }
        }
    }
}
