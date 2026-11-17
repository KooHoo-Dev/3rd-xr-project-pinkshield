using CardShop.Core;
using TMPro;
using UnityEngine;

namespace CardShop.Decor
{
    /// <summary>집 벽 달력: "N일차 / 전체" (인테리어 설계 H4). 아침 "N일차" 카드와 짝을 이룹니다.</summary>
    public sealed class DayCalendar : PhaseListener
    {
        [SerializeField] TMP_Text label;

        protected override void Refresh()
        {
            if (label == null || Context == null || Context.Session == null) return;
            label.text = $"<size=55%>DAY</size>\n{Context.Session.Day}\n<size=45%>/ {Context.Campaign.DayCount}일</size>";
        }
    }
}
