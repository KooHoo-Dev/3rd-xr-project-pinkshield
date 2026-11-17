using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>
    /// 계산대의 열린 종이봉투 (계산시스템_확장_설계 2장 ③ · 6장). 찍은 물건을 봉투 위에서 놓으면 안쪽 바닥부터 차례로 붙입니다.
    /// 물리로 쌓지 않고 정해진 자리에 놓기만 합니다(튀거나 떨림 없음). 담기 판정 · 안내는 CheckoutCounter가 합니다.
    /// 원점 = 봉투 안쪽 바닥 가운데, 크기 1로 둡니다(벽은 자식 메시).
    /// </summary>
    public sealed class PaperBag : MonoBehaviour
    {
        [Tooltip("봉투 안쪽 크기 (m, 가로 · 높이 · 깊이)")]
        [SerializeField] Vector3 innerSize = new Vector3(0.28f, 0.2f, 0.18f);
        [Tooltip("입구 위로 이만큼까지는 '봉투에 놓음'으로 봄 (m). 위에서 떨어뜨려 넣는 동작")]
        [SerializeField] float dropAbove = 0.15f;
        [Tooltip("가로 · 깊이 방향 여유 (m). 벽에 걸쳐 놓아도 담기게")]
        [SerializeField] float sideMargin = 0.04f;

        float fill; // 지금까지 쌓인 높이

        public Vector3 InnerSize => innerSize;

        /// <summary>이 월드 위치에서 물건을 놓으면 봉투에 담는가.</summary>
        public bool Contains(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return Mathf.Abs(p.x) <= innerSize.x / 2f + sideMargin
                && Mathf.Abs(p.z) <= innerSize.z / 2f + sideMargin
                && p.y >= -0.03f && p.y <= innerSize.y + dropAbove;
        }

        /// <summary>물건을 봉투 안 바닥 가운데부터 위로 쌓습니다. 방향은 계산대에 놓였던 방향 그대로.</summary>
        public void Put(ScannableItem item)
        {
            Vector3 floor = transform.TransformPoint(new Vector3(0f, fill + 0.002f, 0f));
            item.PutInBag(floor, item.HomeRotation);
            fill += item.Height + 0.002f;
        }

        /// <summary>손님이 봉투를 가져감: 다음 손님을 위해 빈 봉투로.</summary>
        public void Clear() => fill = 0f;
    }
}
