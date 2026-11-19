using CardShop.Customers;
using TMPro;
using UnityEngine;

namespace CardShop.Interaction
{
    /// <summary>
    /// 영업 중 계산대의 "오늘 무료 팩"(도둑 · 잡기 전용, ThiefDirector)의 겉모습을 선반 팩 프리팹과 같게 바꿉니다 (11-20 A1).
    /// 예전 금색 상자는 숨기고, 선반 팩 프리팹을 복사한 뒤 보이는 것(메시 · 글자)만 남겨 붙입니다. 잡기 · 콜라이더 · 도둑 동작은 원래 팩 그대로.
    /// 퇴근 후 FreePackOffer가 같은 자리에 같은 프리팹을 놓으므로, 영업 중과 퇴근 후 모양이 같아집니다.
    /// 씬을 다시 빌드하지 않아도 되게 씬이 열리면 스스로 생깁니다.
    /// </summary>
    public sealed class FreePackLook : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<ThiefDirector>() == null || FindAnyObjectByType<FreePackLook>() != null) return;
            new GameObject("FreePackLook").AddComponent<FreePackLook>();
        }

        void Start()
        {
            var thief = FindAnyObjectByType<ThiefDirector>();
            var shelf = FindAnyObjectByType<ShelfController>();
            if (thief != null && shelf != null && thief.FreePack != null && shelf.PackPrefab != null)
                Apply(thief.FreePack, shelf.PackPrefab);
            Destroy(gameObject);
        }

        /// <summary>pack의 지금 렌더러를 끄고, prefab의 보이는 부분만 복사해 pack 밑에 붙입니다.</summary>
        public static void Apply(Transform pack, Component prefab)
        {
            foreach (Renderer r in pack.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            var holder = new GameObject("PackLook");
            holder.SetActive(false); // 복사본의 Awake(잡기 · 팩 스크립트)가 돌지 않게 꺼 둔 채로 복사
            holder.transform.SetParent(pack, false);
            Component copy = Instantiate(prefab, holder.transform, false);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true)) StripToLook(t.gameObject);
            holder.SetActive(true);
        }

        // 메시 · 글자만 남김. 의존하는 컴포넌트(뒤에 붙은 것)부터 지워야 RequireComponent에 막히지 않음
        static void StripToLook(GameObject go)
        {
            Component[] all = go.GetComponents<Component>();
            for (int i = all.Length - 1; i >= 0; i--)
            {
                Component c = all[i];
                if (c == null || c is Transform || c is MeshFilter || c is Renderer || c is TMP_Text || c is TMP_SubMesh) continue;
                DestroyImmediate(c);
            }
        }
    }
}
