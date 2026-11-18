using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>도둑 몸 꾸미기 (카드도둑_설계 6장): 키 재기 · 복면 씌우기 · 손 뼈 찾기.</summary>
    public static class ThiefBody
    {
        /// <summary>발에서 머리 위까지 (빗자루 맞음 판정 높이). 머리 뼈가 없으면 1.7m.</summary>
        public static float Height(CustomerAgent thief, Transform head) =>
            head != null ? head.position.y - thief.transform.position.y + 0.15f : 1.7f;

        /// <summary>복면 원본을 복제해 머리 뼈에 붙임 (지금 몸이 보는 쪽이 앞).</summary>
        /// <param name="headOffset">복면 가운데를 머리 뼈에서 이만큼 위로 (m, 손님 키 배율 전)</param>
        public static void WearMask(CustomerAgent thief, Transform head, GameObject template, float headOffset, float height)
        {
            if (template == null) return;
            GameObject mask = Object.Instantiate(template);
            mask.name = "ThiefMask";
            float scale = thief.transform.lossyScale.x;
            mask.transform.localScale = template.transform.localScale * scale;
            Vector3 center = head != null
                ? head.position + Vector3.up * headOffset * scale
                : thief.transform.position + Vector3.up * (height - 0.15f);
            // 머리 뼈 축은 모델마다 달라서 몸 방향으로 맞춘 뒤 머리에 붙임
            mask.transform.SetPositionAndRotation(center, Quaternion.LookRotation(thief.transform.forward, Vector3.up));
            mask.transform.SetParent(head != null ? head : thief.transform, true);
            mask.SetActive(true);
        }

        /// <summary>오른손 뼈 (휴머노이드가 아니면 몸).</summary>
        public static Transform Hand(CustomerAgent agent)
        {
            var animator = agent.GetComponentInChildren<Animator>();
            Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            return hand != null ? hand : agent.transform;
        }
    }
}
