using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CardShop.Interaction
{
    /// <summary>
    /// 어느 손으로 했는지 모를 때 양손을 함께 짧게 울립니다 (보관함 꽂기 · 튜토리얼 자막 · 컴퓨터 쪽 넘기기).
    /// 누른 손을 아는 곳은 그 손의 SendHapticImpulse를 바로 씁니다(세기를 곳마다 일부러 다르게 둠).
    /// </summary>
    public static class Haptics
    {
        public static void BothHands(float amplitude, float seconds)
        {
            foreach (XRBaseInputInteractor hand in Object.FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None))
                hand.SendHapticImpulse(amplitude, seconds);
        }
    }
}
