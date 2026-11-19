using CardShop.Core;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 계산대 사장님 메모(CounterMemo)가 놓일 자리 표시 (11-20 J3: 메모가 코드 값으로만 놓여 씬에서 옮길 수 없었음).
    /// 씬에 이 컴포넌트가 붙은 빈 오브젝트가 있으면 메모가 그 위치 · 방향에 놓입니다. 없으면 <see cref="DefaultPose"/>.
    /// 만들기: Tools > CardShop > Tutorial > Create Memo Anchor (기본 자리에 만들어 선택해 줌 → 씬에서 옮기면 됨).
    /// 방향: 오브젝트의 앞(+Z)이 글자를 읽는 쪽의 반대 (기본은 플레이어 쪽 위로 60° 세운 판).
    /// </summary>
    public sealed class TutorialMemoAnchor : MonoBehaviour
    {
        /// <summary>봉투 오른쪽 계산대 위 (예전 자리 x 0.45는 봉투와 겹쳤음)</summary>
        public static Pose DefaultPose => new Pose(
            new Vector3(0.88f, ShopLayout.CounterTop + 0.075f, 0.52f),
            Quaternion.LookRotation(new Vector3(0f, -0.5f, 0.866f), Vector3.up));

        /// <summary>씬의 메모 자리. 없으면 기본 자리</summary>
        public static Pose Find()
        {
            var anchor = FindAnyObjectByType<TutorialMemoAnchor>();
            return anchor != null ? new Pose(anchor.transform.position, anchor.transform.rotation) : DefaultPose;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.4f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.2f, 0.14f, 0.005f));
        }
    }
}
