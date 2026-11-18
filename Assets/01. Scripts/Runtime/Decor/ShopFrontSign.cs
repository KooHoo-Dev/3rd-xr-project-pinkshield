using System.Text;
using CardShop.UI;
using TMPro;
using UnityEngine;

namespace CardShop.Decor
{
    /// <summary>
    /// 가게 정면 큰 간판 "Slime CardShop" (DEC-027 간판 A). 글자를 앞에서부터 몇 개까지 켤지 정할 수 있어
    /// 타이틀 첫 등장 연출(TitleIntro)이 네온 점등처럼 하나씩 켭니다. 평소에는 모두 켜짐.
    /// </summary>
    public sealed class ShopFrontSign : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] string shopName = "Slime CardShop";
        [Tooltip("첫 단어(Slime) 색")]
        [SerializeField] Color firstWordColor = UiTheme.Accent;
        [Tooltip("나머지 글자 색")]
        [SerializeField] Color restColor = UiTheme.Gold;
        [Tooltip("꺼진 글자 색")]
        [SerializeField] Color offColor = new Color(0.32f, 0.26f, 0.4f);

        readonly StringBuilder sb = new StringBuilder();

        public int Length => shopName.Length;

        void Awake() => SetLit(Length);

        /// <summary>앞에서부터 count글자만 켭니다.</summary>
        public void SetLit(int count)
        {
            if (label == null) return;
            int firstWord = shopName.IndexOf(' ');
            sb.Clear();
            for (int i = 0; i < shopName.Length; i++)
            {
                Color c = i >= count ? offColor : (firstWord < 0 || i < firstWord) ? firstWordColor : restColor;
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append('>').Append(shopName[i]).Append("</color>");
            }
            label.text = sb.ToString();
        }
    }
}
