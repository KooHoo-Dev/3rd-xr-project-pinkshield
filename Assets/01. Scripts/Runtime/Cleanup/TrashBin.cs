using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 쓰레기통 (인테리어 설계 S5). 원점 = 통 바닥 가운데. 쓰레기를 통 위쪽 원기둥 안에서 놓으면 버린 것으로 봅니다
    /// (소켓 대신 위치 판정: 종이봉투와 같은 방식, 위에서 떨어뜨려 넣는 동작).
    /// </summary>
    public sealed class TrashBin : MonoBehaviour
    {
        [Tooltip("통 입구 반지름 + 여유 (m)")]
        [SerializeField] float radius = 0.32f;
        [Tooltip("통 바닥에서 이 높이까지 놓으면 버림 (m). 허리 높이에서 떨어뜨려도 되게")]
        [SerializeField] float dropHeight = 1.3f;

        public bool Contains(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return new Vector2(p.x, p.z).sqrMagnitude <= radius * radius && p.y >= -0.05f && p.y <= dropHeight;
        }
    }
}
