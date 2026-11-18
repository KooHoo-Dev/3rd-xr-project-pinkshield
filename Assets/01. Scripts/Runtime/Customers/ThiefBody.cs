using UnityEngine;

namespace CardShop.Customers
{
    /// <summary>도둑 몸 꾸미기 (카드도둑_설계 6장): 키 재기 · 복면 씌우기 · 검은 옷 · 손 뼈 찾기.</summary>
    public static class ThiefBody
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock block;

        /// <summary>
        /// 온몸을 어둡게 칠해 만화 속 도둑처럼 검은 옷 + 장갑으로 (11-19 피드백: 손님과 구분이 잘 안 됨).
        /// 손님 머티리얼은 아틀라스 한 장이라 색을 곱해서 어둡게 함 (머티리얼을 복제하지 않음 — MaterialPropertyBlock).
        /// 복면은 이 함수 뒤에 씌우므로 영향받지 않습니다.
        /// </summary>
        public static void DressDark(CustomerAgent thief, Color tint)
        {
            block ??= new MaterialPropertyBlock();
            foreach (Renderer r in thief.GetComponentsInChildren<Renderer>(true))
            {
                if (r is not SkinnedMeshRenderer && r is not MeshRenderer) continue; // 말풍선(스프라이트 · 글자)은 그대로
                r.GetPropertyBlock(block);
                block.SetColor(ColorId, tint);
                block.SetColor(BaseColorId, tint);
                r.SetPropertyBlock(block);
            }
        }

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
