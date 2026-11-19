using UnityEngine;

namespace CardShop.Core
{
    /// <summary>
    /// 실행 중에 생기는 물건의 자리표 (맵_수정_도구_설계 2장). 씬에 이 표가 있으면 물건이 그 위치 · 방향에 놓이고,
    /// 없으면 코드에 적힌 기본 자리에 놓입니다. 그래서 자리표를 지워도 예전과 똑같이 동작합니다.
    ///
    /// 만들기 · 옮기기: Tools > CardShop > Map Edit 창. 도구를 켜면 자리표 밑에 크기 모형이 떠서
    /// 실제 물건 크기를 보며 옮길 수 있습니다(모형은 씬 파일에 저장되지 않음).
    /// 모형을 클릭해도 자리표가 선택되도록 <see cref="SelectionBaseAttribute"/>를 붙였습니다.
    /// </summary>
    [SelectionBase, DisallowMultipleComponent]
    public sealed class PlacementAnchor : MonoBehaviour
    {
        [Tooltip("물건 코드가 이 이름으로 자리표를 찾습니다. 씬 안에서 겹치지 않아야 합니다.")]
        public string key;

        [Header("미리보기 모형 (자리표 기준 로컬)")]
        public Vector3 previewCenter;
        public Vector3 previewSize = new Vector3(0.3f, 0.3f, 0.05f);

        /// <summary>키가 같은 자리표를 찾습니다 (꺼진 것 포함).</summary>
        public static PlacementAnchor Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (PlacementAnchor a in FindObjectsByType<PlacementAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (a.key == key) return a;
            return null;
        }

        /// <summary>자리표가 있으면 그 월드 위치 · 방향, 없으면 fallback</summary>
        public static Pose Find(string key, Pose fallback)
        {
            PlacementAnchor a = Get(key);
            return a != null ? new Pose(a.transform.position, a.transform.rotation) : fallback;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(previewCenter, previewSize);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.15f); // 앞(+Z) 방향
        }
    }
}
