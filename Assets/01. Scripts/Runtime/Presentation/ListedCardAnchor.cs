using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 가게 진열대 판매 등록 카드 줄의 자리 (11-21 피드백 P1). 빈 오브젝트에 붙여 씬 · ShopInterior 프리팹에서 옮기면 됩니다.
    /// 위치 = 줄 가운데의 카드 받침 바닥, forward = 카드 앞면이 보는 쪽(보통 계산대 · 플레이어 쪽).
    /// 없으면 StandListedCards가 SalesStand 윗면 앞쪽에 스스로 한 줄을 잡습니다.
    /// </summary>
    public sealed class ListedCardAnchor : MonoBehaviour
    {
        [Tooltip("카드를 늘어놓는 줄 폭 (m). 칸 수가 많으면 이 폭 안에 나눠 섬")]
        public float rowWidth = 1.3f;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.75f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.045f, 0f), new Vector3(rowWidth, 0.09f, 0.01f));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.1f);
        }
    }
}
