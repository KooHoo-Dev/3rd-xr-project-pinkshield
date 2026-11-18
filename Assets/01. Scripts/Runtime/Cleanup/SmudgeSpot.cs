using CardShop.Cards;
using CardShop.Logic;
using UnityEngine;

namespace CardShop.Cleanup
{
    /// <summary>
    /// 창문 얼룩 (기획서 5-1 · 설계서 7-4). 걸레를 든 채 얼룩 위에서 움직인 누적 거리로 진행도를 셉니다.
    /// 지워질수록 얼룩이 작아집니다(투명 셰이더 없이 Quest에서 가볍게). 원점 = 얼룩 가운데, forward = 창 안쪽(가게 쪽).
    /// </summary>
    public sealed class SmudgeSpot : CleanupSpot
    {
        [Tooltip("얼룩 모양 (작아지는 부분)")]
        [SerializeField] Transform visual;
        [Tooltip("얼룩 반지름 + 여유 (m). 이 안에서 문질러야 셈")]
        [SerializeField] float radius = 0.2f;
        [Tooltip("창 면에서 이만큼 떨어져 있어도 문지르는 것으로 봄 (m)")]
        [SerializeField] float reach = 0.12f;
        [Tooltip("다 지우는 데 필요한 문지른 거리 (m). 15cm씩 약 8번")]
        [SerializeField] float requiredMeters = 1.2f;

        CleaningRag rag;
        Vector3 fullScale;
        Vector3 lastLocal;
        bool rubbing;
        float rubbed;

        public override CleanupKinds Kind => CleanupKinds.Smudge;
        public float Progress => CleanupRules.SmudgeProgress(rubbed, requiredMeters);

        public void Init(CleaningRag cleaningRag)
        {
            rag = cleaningRag;
            if (visual != null) fullScale = visual.localScale;
        }

        void Update()
        {
            if (IsDone || rag == null || !rag.IsHeld) { rubbing = false; return; }

            Vector3 local = transform.InverseTransformPoint(rag.WipePoint);
            bool onSpot = Mathf.Abs(local.z) <= reach && new Vector2(local.x, local.y).sqrMagnitude <= radius * radius;
            if (!onSpot) { rubbing = false; return; }

            if (rubbing)
            {
                Vector2 move = new Vector2(local.x - lastLocal.x, local.y - lastLocal.y); // 창 면을 따라 움직인 거리만
                rubbed += Mathf.Min(move.magnitude, 0.1f); // 순간이동 · 추적 튐은 한 번에 10cm까지만
            }
            lastLocal = local;
            rubbing = true;

            if (visual != null) visual.localScale = fullScale * Mathf.Lerp(1f, 0.15f, Progress);
            if (Progress >= 1f)
            {
                gameObject.SetActive(false);
                Complete();
            }
        }
    }
}
