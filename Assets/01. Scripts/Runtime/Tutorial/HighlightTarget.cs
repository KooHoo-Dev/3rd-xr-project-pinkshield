using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼이 가리킬 수 있는 씬 물건에 붙이는 이름표(<see cref="key"/>).
    /// 켜져 있는 것만 정적 목록에 등록되고, 튜토리얼은 이름 검색 대신 이 목록에서 key로 찾습니다 (설계서 7-5).
    /// 한 물건에 key가 다른 이름표를 여러 개 붙여도 됩니다.
    /// </summary>
    public sealed class HighlightTarget : MonoBehaviour
    {
        const float FallbackSize = 0.1f; // Renderer가 없을 때 쓰는 상자 (m)

        static readonly List<HighlightTarget> active = new List<HighlightTarget>();

        [Tooltip("튜토리얼 단계의 targets에 적는 이름")]
        public string key;

        Renderer[] renderers;

        /// <summary>물건이 하이어라키에서 켜져 있는가.</summary>
        public bool IsAlive => this != null && gameObject.activeInHierarchy;

        /// <summary>key가 같은 켜진 이름표를 results 뒤에 덧붙입니다.</summary>
        public static void Find(string key, List<HighlightTarget> results)
        {
            if (string.IsNullOrEmpty(key) || results == null) return;
            for (int i = 0; i < active.Count; i++)
                if (active[i] != null && active[i].key == key) results.Add(active[i]);
        }

        /// <summary>같은 key의 이름표가 이미 있으면 그것을, 없으면 새로 붙여 돌려줍니다.</summary>
        public static HighlightTarget Mark(GameObject go, string key)
        {
            if (go == null) return null;
            var existing = go.GetComponents<HighlightTarget>();
            foreach (HighlightTarget t in existing)
                if (t.key == key) return t;
            var added = go.AddComponent<HighlightTarget>();
            added.key = key;
            return added;
        }

        /// <summary>on이면 이름표를 붙이고(Mark), 아니면 같은 key의 이름표를 떼어 냅니다 (바로 목록에서 빠짐).</summary>
        public static void SetMarked(GameObject go, string key, bool on)
        {
            if (go == null) return;
            if (on) { Mark(go, key); return; }
            foreach (HighlightTarget t in go.GetComponents<HighlightTarget>())
                if (t.key == key) { t.enabled = false; Destroy(t); } // Destroy는 프레임 끝에 되므로 먼저 꺼서 Find에서 빠지게
        }

        void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            RefreshRenderers();
        }

        void OnDisable() => active.Remove(this);

        /// <summary>자식 Renderer (꺼진 것 포함). 외곽선용</summary>
        public IReadOnlyList<Renderer> Renderers
        {
            get { if (renderers == null || HasMissingRenderer()) RefreshRenderers(); return renderers; }
        }

        /// <summary>자식 Renderer 목록을 다시 모읍니다 (모양이 바뀌었을 때).</summary>
        public void RefreshRenderers() => renderers = GetComponentsInChildren<Renderer>(true);

        /// <summary>
        /// 켜진 자식 Renderer bounds의 합. Renderer가 없으면 자리에 10cm 상자.
        /// 물건이 꺼져 있으면 false.
        /// </summary>
        public bool TryGetBounds(out Bounds b)
        {
            b = default;
            if (!IsAlive) return false;
            if (renderers == null || HasMissingRenderer()) RefreshRenderers();

            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (found) b.Encapsulate(r.bounds);
                else { b = r.bounds; found = true; }
            }
            if (!found) b = new Bounds(transform.position, Vector3.one * FallbackSize);
            return true;
        }

        // 캐시한 Renderer 중 파괴된 것이 있는가
        bool HasMissingRenderer()
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] == null) return true;
            return false;
        }
    }
}
