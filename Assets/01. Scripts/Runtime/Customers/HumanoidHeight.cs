using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>사람 모델의 키 맞추기 (손님 · 행인 공용). 머리 뼈 높이가 목표가 되도록 균일 축소/확대합니다.</summary>
    public static class HumanoidHeight
    {
        /// <returns>적용한 배율 (머리 뼈를 못 찾으면 1)</returns>
        public static float Fit(GameObject go, Animator animator, float targetHeadHeight)
        {
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            float current = head != null ? head.position.y - go.transform.position.y : 0f;
            if (current < 0.5f) return 1f; // 뼈를 못 찾음: 크기 그대로

            float scale = targetHeadHeight / current;
            go.transform.localScale *= scale;
            return scale;
        }

        public static Transform Head(Animator animator) =>
            animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
    }
}
