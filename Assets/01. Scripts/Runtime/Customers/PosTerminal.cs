using System.Text;
using CardShop.Logic;
using TMPro;
using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 계산대 포스기 화면 (계산시스템_확장_설계 2장 ②). 찍은 물건이 한 줄씩 쌓이고 합계가 자동으로 계산됩니다.
    /// 다 찍으면 "봉투에 담기 n / m", 다 담으면 "결제 버튼을 눌러 주세요"로 바뀝니다.
    /// 화면 표시만 맡고, 결제 판정은 CheckoutCounter가 합니다.
    /// </summary>
    public sealed class PosTerminal : MonoBehaviour
    {
        [SerializeField] TMP_Text screen;
        [SerializeField] string idleText = "어서 오세요!";

        readonly StringBuilder sb = new StringBuilder();

        void Awake() => Clear();

        public void Show(Basket basket)
        {
            if (screen == null || basket == null) return;
            sb.Clear();
            foreach (BasketLine line in basket.Lines)
                if (line.Scanned) sb.Append(line.Name).Append("<pos=72%>").Append(line.Price).Append('\n');

            sb.Append("<size=120%><b>합계<pos=72%>").Append(basket.ScannedTotal).Append("</b></size>\n");
            if (basket.AllBagged) sb.Append("<color=#7CFC9A>결제 버튼을 눌러 주세요</color>");
            else if (basket.AllScanned) sb.Append("<color=#FFD24D>봉투에 담기 ").Append(basket.BaggedCount).Append(" / ").Append(basket.Count).Append("</color>");
            else sb.Append("<color=#FFD24D>바코드 ").Append(basket.ScannedCount).Append(" / ").Append(basket.Count).Append("</color>");
            screen.text = sb.ToString();
        }

        public void Clear()
        {
            if (screen != null) screen.text = idleText;
        }
    }
}
