using CardShop.Core;
using TMPro;
using UnityEngine;

namespace CardShop.Decor
{
    /// <summary>출입구 위 OPEN / CLOSED 팻말 (인테리어 설계 S2). 영업 중(1일차 인트로 포함)에만 OPEN, 정산부터 CLOSED.</summary>
    public sealed class ShopSign : PhaseListener
    {
        [SerializeField] TMP_Text label;
        [SerializeField] Renderer board;
        [SerializeField] Color openColor = new Color(0.3f, 0.85f, 0.45f);
        [SerializeField] Color closedColor = new Color(0.85f, 0.3f, 0.3f);

        MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        protected override void Refresh()
        {
            bool open = CurrentPhase == GamePhaseId.Business || CurrentPhase == GamePhaseId.Intro;
            if (label != null) label.text = open ? "OPEN\n<size=60%>영업 중</size>" : "CLOSED\n<size=60%>영업 종료</size>";
            if (board != null)
            {
                block ??= new MaterialPropertyBlock();
                board.GetPropertyBlock(block);
                block.SetColor(BaseColorId, open ? openColor : closedColor);
                board.SetPropertyBlock(block);
            }
        }
    }
}
