using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 한 단계를 "보여 주는" 쪽 묶음: 말풍선(BossBubble) · 대상 하이라이트(TargetHighlighter) · 시야 밖 화살표(OffscreenArrow).
    /// 진행(언제 무엇을)은 TutorialDirector가 정하고, 이 클래스는 받은 단계의 대상 키를 찾아 그리기만 합니다.
    /// 시야 밖 화살표는 행동을 기다리는 단계(기다림 · 정지)에만 씁니다 (튜토리얼_설계 7장).
    /// </summary>
    public sealed class TutorialView : MonoBehaviour
    {
        BossBubble bubble;
        TargetHighlighter highlighter;
        OffscreenArrow arrow;
        readonly List<HighlightTarget> targets = new List<HighlightTarget>();
        TutorialStep step;

        public bool IsShowing => step != null;

        public void Configure(TMP_FontAsset font)
        {
            bubble = Child<BossBubble>("BossBubble");
            bubble.Configure(font);
            highlighter = Child<TargetHighlighter>("TargetHighlighter");
            arrow = Child<OffscreenArrow>("OffscreenArrow");
        }

        T Child<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        /// <summary>단계를 보여 줍니다. text는 자리표시를 채운 대사(손 힌트가 붙을 수 있음).</summary>
        public void Show(TutorialStep s, string text)
        {
            step = s;
            Retarget();
            bubble.Show(s.speaker, text, targets.Count > 0 ? (System.Func<Bounds?>)TargetBounds : null);
            bool wantsAction = s.kind != TutorialStepKind.Say;
            arrow.SetTarget(wantsAction && targets.Count > 0 ? (System.Func<Vector3?>)TargetCenter : null);
        }

        /// <summary>같은 단계의 말만 바꿈 (손 힌트 덧붙이기 등)</summary>
        public void ChangeText(string text)
        {
            if (step == null) return;
            bubble.Show(step.speaker, text, targets.Count > 0 ? (System.Func<Bounds?>)TargetBounds : null);
        }

        /// <summary>대상 키로 하이라이트 대상을 다시 찾습니다 (새로 생긴 쓰레기 · 도둑 등)</summary>
        public void Retarget()
        {
            targets.Clear();
            if (step == null || step.targets == null) { highlighter.Hide(); return; }
            foreach (string key in step.targets)
                if (!string.IsNullOrEmpty(key)) HighlightTarget.Find(key, targets);
            if (targets.Count > 0) highlighter.Show(targets);
            else highlighter.Hide();
        }

        public void Hide()
        {
            step = null;
            targets.Clear();
            if (bubble != null) bubble.Hide();
            if (highlighter != null) highlighter.Hide();
            if (arrow != null) arrow.SetTarget(null);
        }

        /// <summary>엉뚱한 물건을 잡았을 때: 대상을 한 번 크게 깜빡임</summary>
        public void Flash() => highlighter?.Flash();

        /// <summary>헤드셋을 다시 썼을 때 등: 대상 없는 말풍선을 다시 눈앞으로</summary>
        public void Reposition() => bubble?.Reposition();

        /// <summary>지금 단계의 대상 Transform들을 into에 담습니다 (먼저 비움). 잠그기에서 남길 물건.</summary>
        public void CollectTargets(List<Transform> into)
        {
            into.Clear();
            foreach (HighlightTarget t in targets)
                if (t != null) into.Add(t.transform);
        }

        /// <summary>held가 지금 단계의 대상(또는 그 일부)인가</summary>
        public bool IsTarget(Transform held)
        {
            foreach (HighlightTarget t in targets)
                if (t != null && TutorialGrabWatcher.Related(held, t.transform)) return true;
            return false;
        }

        Bounds? TargetBounds()
        {
            bool any = false;
            Bounds all = default;
            foreach (HighlightTarget t in targets)
            {
                if (t == null || !t.TryGetBounds(out Bounds b)) continue;
                if (!any) { all = b; any = true; }
                else all.Encapsulate(b);
            }
            return any ? all : (Bounds?)null;
        }

        Vector3? TargetCenter()
        {
            foreach (HighlightTarget t in targets)
                if (t != null && t.TryGetBounds(out Bounds b)) return b.center;
            return null;
        }
    }
}
