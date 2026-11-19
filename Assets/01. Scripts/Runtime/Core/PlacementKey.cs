using UnityEngine;

namespace CardShop.Core
{
    /// <summary>
    /// 씬 빌더(Build Shop Scene)가 만든 오브젝트 중 "사람이 씬에서 손으로 옮겨도 되는 것"에 붙는 표식 (2026-11-18 기기 테스트 피드백).
    /// 빌더는 씬을 새로 만들기 전에 이 표식이 붙은 오브젝트가 빌더 기본 자리(<see cref="defaultLocalPosition"/>)에서
    /// 옮겨졌는지 보고, 옮겨진 것만 기억했다가 새 씬에 그대로 되돌려 놓습니다 (Editor/Scenes/PlacementMemory).
    ///
    /// 위치는 부모 기준(로컬)으로 비교하므로, 부모(예: 계산대 묶음)를 옮기면 자식(물건 자리 · 봉투)은 함께 따라가고
    /// 부모만 "옮긴 것"으로 기억됩니다. 게임 실행 중에는 아무 일도 하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacementKey : MonoBehaviour
    {
        [Tooltip("빌더가 다시 만들어도 같은 오브젝트를 찾는 이름. 씬 안에서 겹치지 않아야 합니다.")]
        public string key;

        [Tooltip("맵 수정 도구에서 손으로 붙인 이름표 (빌더가 모르는 오브젝트). 키가 계층 경로라 Build Shop Scene 뒤 같은 경로를 찾아 자리를 되살림")]
        public bool addedByHand;

        [Header("빌더 기본 자리 (손대지 마세요: 옮겼는지 판단하는 기준)")]
        public Vector3 defaultLocalPosition;
        public Quaternion defaultLocalRotation = Quaternion.identity;
        public Vector3 defaultLocalScale = Vector3.one;

        /// <summary>지금 자리가 빌더 기본 자리와 다른가 (1mm · 0.1도 · 배율 0.1% 넘게)</summary>
        public bool IsMoved =>
            (transform.localPosition - defaultLocalPosition).sqrMagnitude > 0.001f * 0.001f ||
            Quaternion.Angle(transform.localRotation, defaultLocalRotation) > 0.1f ||
            (transform.localScale - defaultLocalScale).sqrMagnitude > 0.001f * 0.001f;

        /// <summary>지금 자리를 빌더 기본 자리로 기록합니다 (빌더가 다 만든 직후 호출).</summary>
        public void RecordDefault()
        {
            defaultLocalPosition = transform.localPosition;
            defaultLocalRotation = transform.localRotation;
            defaultLocalScale = transform.localScale;
        }

        /// <summary>오브젝트에 표식을 붙입니다 (이미 있으면 이름만 바꿈).</summary>
        public static PlacementKey Mark(GameObject go, string key)
        {
            if (go == null) return null;
            var mark = go.GetComponent<PlacementKey>();
            if (mark == null) mark = go.AddComponent<PlacementKey>();
            mark.key = key;
            return mark;
        }

        /// <summary>
        /// 빌더가 이름표를 붙이지 않은 오브젝트에 손으로 이름표를 붙입니다 (맵 수정 도구). 키 = 계층 경로.
        /// 빌더 기본 자리를 알 수 없으므로 지금 자리를 기본으로 두고, Build Shop Scene 때는 옮겼는지와 상관없이 늘 기억합니다.
        /// </summary>
        public static PlacementKey MarkByHand(GameObject go)
        {
            PlacementKey mark = Mark(go, HierarchyPath.Of(go.transform));
            mark.addedByHand = true;
            mark.RecordDefault();
            return mark;
        }

        /// <summary>Build Shop Scene 전에 자리를 기억해야 하는가: 옮겼거나 손으로 붙인 이름표</summary>
        public bool ShouldRemember => addedByHand || IsMoved;
    }
}
