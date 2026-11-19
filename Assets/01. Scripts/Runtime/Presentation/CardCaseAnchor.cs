using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 카드 보관함(CardCase) 자리 표시 (카드보관함_설계 Q2 · J6). 씬에 이 컴포넌트가 붙은 빈 오브젝트가 있으면 보관함이 그 위치 · 방향에 붙습니다.
    /// 없으면 컴퓨터 모니터 위 벽. 방향: 오브젝트의 앞(+Z)이 방 쪽(카드 앞면이 보이는 쪽).
    /// 만들기: Tools > CardShop > Home > Create Card Case Anchor
    /// </summary>
    public sealed class CardCaseAnchor : MonoBehaviour
    {
        public static bool TryFind(out Pose pose)
        {
            var a = FindAnyObjectByType<CardCaseAnchor>();
            pose = a != null ? new Pose(a.transform.position, a.transform.rotation) : default;
            return a != null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.7f, 0.4f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.56f, 0.38f, 0.08f));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.2f);
        }
    }
}
