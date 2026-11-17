using UnityEngine.XR.Interaction.Toolkit;

namespace CardShop.Interaction
{
    /// <summary>
    /// XRI Interaction Layer 번호 모음 (물리 레이어와 다름). 이름은
    /// Assets/XRI/Settings/Resources/InteractionLayerSettings.asset 의 같은 번호 칸에 적어 둡니다.
    /// 카드는 Default + Card, 바인더 표지 소켓은 Card만 → 팩 같은 다른 물건은 표지에 꽂히지 않습니다.
    /// </summary>
    public static class CardInteractionLayers
    {
        /// <summary>InteractionLayerSettings의 4번째 칸(번호 3) = "Card"</summary>
        public const int CardIndex = 3;

        public static InteractionLayerMask Card => 1 << CardIndex;

        /// <summary>손(Default)으로도 잡히고 바인더 표지(Card)에도 꽂히는 카드용 마스크.</summary>
        public static InteractionLayerMask DefaultAndCard => 1 | (1 << CardIndex);
    }
}
