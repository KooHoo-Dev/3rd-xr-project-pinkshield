using CardShop.Logic;
using UnityEngine;

namespace CardShop.UI
{
    /// <summary>
    /// 정산 도장 그림 · 잉크 색 (11-20 플레이 F2, Q8: 한 색 그림을 코드에서 물들임).
    /// 그림은 Resources/Stamps/Stamp_Great · Good · Weak (흰 선 + 투명, DevLog/tools/stamp-ink.ps1로 만듦).
    /// 그림이 없으면 null → ReceiptPrinter가 예전 분홍 고리 + 글자 도장을 그대로 씀.
    /// </summary>
    public static class StampArt
    {
        const string Folder = "Stamps/";
        public static readonly Color GreatInk = UiTheme.Stamp;                  // 분홍
        public static readonly Color GoodInk = new Color(0.95f, 0.55f, 0.2f);   // 주황
        public static readonly Color WeakInk = new Color(0.5f, 0.5f, 0.55f);    // 회색

        /// <summary>도장 문구(SettlementStamp) → 그림 이름</summary>
        public static string FileFor(string word) =>
            word == SettlementStamp.Great ? "Stamp_Great" : word == SettlementStamp.Good ? "Stamp_Good" : "Stamp_Weak";

        public static Color InkFor(string word) =>
            word == SettlementStamp.Great ? GreatInk : word == SettlementStamp.Good ? GoodInk : WeakInk;

        /// <summary>그 문구의 도장 그림 (없으면 null)</summary>
        public static Sprite SpriteFor(string word) => Resources.Load<Sprite>(Folder + FileFor(word));
    }
}
