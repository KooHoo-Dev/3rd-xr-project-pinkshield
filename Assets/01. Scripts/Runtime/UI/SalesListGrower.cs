using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardShop.UI
{
    /// <summary>
    /// 판매 패널의 "판매 등록 중" 목록 버튼 늘리기 (바인더 업그레이드: 판매 등록 칸 4 → 6, 집_업그레이드_설계 3장).
    /// 씬 빌더는 칸 수만큼(4개) 버튼을 만들어 두었고 패널에 더 들어갈 줄이 없으므로,
    /// 칸이 늘면 마지막 버튼을 복제하고 목록 전체를 두 줄(왼쪽 · 오른쪽)로 다시 놓습니다. 씬 다시 빌드 불필요.
    /// </summary>
    public static class SalesListGrower
    {
        const float SmallFont = 20f;
        const float ColumnGap = 0.02f;

        /// <summary>buttons가 count보다 적으면 늘린 새 배열을 돌려줍니다(아니면 그대로). onClick(i)는 새 버튼에만 연결합니다.</summary>
        public static Button[] Grow(Button[] buttons, int count, Action<int> onClick)
        {
            if (buttons == null || buttons.Length == 0 || count <= buttons.Length) return buttons;

            // 기존 버튼들이 차지하던 세로 범위 (첫 버튼 위 ~ 마지막 버튼 아래)
            var first = (RectTransform)buttons[0].transform;
            var last = (RectTransform)buttons[buttons.Length - 1].transform;
            float left = first.anchorMin.x, right = first.anchorMax.x;
            float top = first.anchorMax.y, bottom = last.anchorMin.y;
            float rowH = first.anchorMax.y - first.anchorMin.y;
            float gap = buttons.Length > 1 ? ((RectTransform)buttons[0].transform).anchorMin.y - ((RectTransform)buttons[1].transform).anchorMax.y : 0f;

            var grown = new Button[count];
            Array.Copy(buttons, grown, buttons.Length);
            for (int i = buttons.Length; i < count; i++)
            {
                Button b = UnityEngine.Object.Instantiate(buttons[buttons.Length - 1], last.parent);
                b.name = $"Button_판매 등록 {i + 1}";
                b.onClick.RemoveAllListeners();
                int index = i; // 람다가 반복 변수 대신 이 칸 번호를 기억하도록
                b.onClick.AddListener(() => onClick(index));
                grown[i] = b;
            }

            // 두 줄로: 줄 수 = 올림(count / 2), 행 높이는 원래 범위 안에 맞춤
            int rows = (count + 1) / 2;
            float available = top - bottom;
            float h = Mathf.Min(rowH, (available - gap * (rows - 1)) / rows);
            float mid = (left + right) / 2f;
            for (int i = 0; i < count; i++)
            {
                int row = i / 2, col = i % 2;
                float y = top - row * (h + gap);
                var rt = (RectTransform)grown[i].transform;
                rt.anchorMin = new Vector2(col == 0 ? left : mid + ColumnGap / 2f, y - h);
                rt.anchorMax = new Vector2(col == 0 ? mid - ColumnGap / 2f : right, y);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                TMP_Text label = grown[i].GetComponentInChildren<TMP_Text>();
                if (label != null) label.fontSize = SmallFont;
            }
            return grown;
        }
    }
}
