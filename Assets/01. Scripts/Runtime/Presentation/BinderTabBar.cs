using System;
using CardShop.Cards;
using CardShop.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.Presentation
{
    /// <summary>
    /// 바인더 속성 탭 버튼 줄: 누르면 탭 바꾸기 알림, 고른 탭은 노란 굵은 글씨 + 밑줄, 오늘 인기 속성 탭에는 ★.
    /// BinderView에서 떼어 냄 (큰_클래스_나누기_제안 3장). 버튼 순서는 BinderTabs.Order.
    /// </summary>
    public sealed class BinderTabBar
    {
        readonly Button[] buttons;
        readonly Color selectedText;

        /// <param name="tabButtons">BinderTabs.Order 순서의 버튼 (null이면 탭 없음)</param>
        public BinderTabBar(Button[] tabButtons, Color selectedTextColor, Action<BinderTab> picked)
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
        }

        public void Redraw(BinderTab current, CardElement popular)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length && i < BinderTabs.Order.Length; i++)
            {
                if (buttons[i] == null) continue;
                TMP_Text label = buttons[i].GetComponentInChildren<TMP_Text>(true);
                if (label == null) continue;
                BinderTab t = BinderTabs.Order[i];
                bool on = t == current;
                bool hot = popular != CardElement.None && BinderTabs.ToElement(t) == popular;
                label.text = hot ? $"★{BinderTabs.Label(t)}" : BinderTabs.Label(t);
                label.color = on ? selectedText : Color.white;
                label.fontStyle = on ? FontStyles.Bold | FontStyles.Underline : FontStyles.Normal;
            }
        }
    }
}
