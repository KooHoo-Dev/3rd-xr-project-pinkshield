using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 쓰레기통 (인테리어 설계 S5). 원점 = 통 바닥 가운데. 쓰레기를 통 위쪽 원기둥 안에서 놓으면 버린 것으로 봅니다
    /// (소켓 대신 위치 판정: 종이봉투와 같은 방식, 위에서 떨어뜨려 넣는 동작).
    /// 쓰레기를 들고 있는 동안 DropHint로 통 위치를 보여 주고, 놓으면 들어가는 범위면 초록으로 바꿉니다 (3차 기기 피드백).
    /// </summary>
    public sealed class TrashBin : MonoBehaviour
    {
        [Tooltip("통 입구 반지름 + 여유 (m)")]
        [SerializeField] float radius = 0.4f; // 11-20: 쓰레기를 1.5배로 키워 0.32 → 0.4
        [Tooltip("통 바닥에서 이 높이까지 놓으면 버림 (m). 허리 높이에서 떨어뜨려도 되게")]
        [SerializeField] float dropHeight = 1.3f;
        [Tooltip("쓰레기를 들고 있을 때 보이는 표시")]
        [SerializeField] DropHint hint;

        readonly List<Transform> carried = new List<Transform>();

        [Tooltip("플레이어가 통 위로 올라서지 못하게 막는 상자 (가로 · 높이 · 깊이, m). 0이면 안 만듦")]
        [SerializeField] Vector3 climbBlocker = new Vector3(0.5f, 1.6f, 0.5f); // 11-20 플레이 뒤 BUG-021: 1.1 → 1.6 (보험, 머리 위까지는 안 감)

        public void Configure(DropHint dropHint) => hint = dropHint;

        // 이동하다 통 위로 올라타던 것 막기 (11-20 C9). 보이지 않는 상자, 레이에는 덜 걸리게 Ignore Raycast 층
        void Awake()
        {
            if (climbBlocker.sqrMagnitude <= 0f) return;
            var go = new GameObject("ClimbBlocker") { layer = 2 };
            go.transform.SetParent(transform, false);
            var box = go.AddComponent<BoxCollider>();
            box.size = climbBlocker;
            box.center = new Vector3(0f, climbBlocker.y / 2f, 0f);
        }

        /// <summary>일시정지 중이면 true (CleanupDirector가 넣음). 마감 시간이 멈춘 동안 청소를 받지 않음</summary>
        public System.Func<bool> IsPaused { get; set; }

        public bool Contains(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return new Vector2(p.x, p.z).sqrMagnitude <= radius * radius && p.y >= -0.05f && p.y <= dropHeight;
        }

        /// <summary>쓰레기를 손에 쥠 (TrashSpot이 부름)</summary>
        public void BeginCarry(Transform item)
        {
            if (item != null && !carried.Contains(item)) carried.Add(item);
        }

        /// <summary>쓰레기를 놓음 · 버림 · 사라짐</summary>
        public void EndCarry(Transform item) => carried.Remove(item);

        void Update()
        {
            carried.RemoveAll(t => t == null || !t.gameObject.activeInHierarchy);
            if (hint == null) return;
            hint.SetShown(carried.Count > 0);
            bool ready = false;
            foreach (Transform t in carried) if (Contains(t.position)) { ready = true; break; }
            hint.SetReady(ready);
        }
    }
}
