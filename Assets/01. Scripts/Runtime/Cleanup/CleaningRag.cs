using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 창문 청소용 걸레 (기획서 5-1). 들고 얼룩 위를 문지르면 SmudgeSpot이 이동 거리를 셉니다.
    /// 놓으면 잠시 뒤 걸레 자리로 돌아갑니다(HeldItem).
    /// </summary>
    [RequireComponent(typeof(HeldItem))]
    public sealed class CleaningRag : MonoBehaviour
    {
        HeldItem held;

        public bool IsHeld => held != null && held.IsHeld;
        /// <summary>문지르는 면의 가운데 (걸레 원점)</summary>
        public Vector3 WipePoint => transform.position;

        void Awake() => held = GetComponent<HeldItem>();
    }
}
