using System.Collections.Generic;
using UnityEngine;

namespace CardShop.Tutorial
{
    /// <summary>
    /// 튜토리얼 단계 목록과 일차별 카운터 메모 문구 (튜토리얼_설계 8-1).
    /// 단계 순서는 목록 순서입니다. Problems로 key 겹침 · 잘못된 after를 검사합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "CardShop/Config/Tutorial Sequence", fileName = "TutorialSequence")]
    public sealed class TutorialSequence : ScriptableObject
    {
        public List<TutorialStep> steps = new List<TutorialStep>();

        [Tooltip("0번 = 1일차. 빈 문자열이면 그날 메모 없음")]
        public List<string> memoByDay = new List<string>();

        /// <summary>그날 메모. 없으면 빈 문자열.</summary>
        public string Memo(int day)
        {
            int i = day - 1;
            if (memoByDay == null || i < 0 || i >= memoByDay.Count) return string.Empty;
            return memoByDay[i] ?? string.Empty;
        }

        /// <summary>정지(Hold) 단계 수. 정지 남용 검사용.</summary>
        public static int HoldCount(IEnumerable<TutorialStep> steps)
        {
            int n = 0;
            if (steps == null) return 0;
            foreach (TutorialStep s in steps)
                if (s != null && s.kind == TutorialStepKind.Hold) n++;
            return n;
        }

        /// <summary>데이터 문제 목록 (빈 · 겹친 key, 없는 after, 뒤쪽을 가리키는 after). 문제 없으면 빈 목록.</summary>
        public static List<string> Problems(IReadOnlyList<TutorialStep> steps)
        {
            var problems = new List<string>();
            if (steps == null) return problems;

            var firstIndex = new Dictionary<string, int>();
            for (int i = 0; i < steps.Count; i++)
            {
                TutorialStep s = steps[i];
                if (s == null) { problems.Add($"{i}번 단계가 비어 있습니다"); continue; }
                if (string.IsNullOrEmpty(s.key)) { problems.Add($"{i}번 단계의 key가 비어 있습니다"); continue; }
                if (firstIndex.ContainsKey(s.key)) problems.Add($"key '{s.key}'가 겹칩니다 ({firstIndex[s.key]}번 · {i}번)");
                else firstIndex.Add(s.key, i);
            }

            for (int i = 0; i < steps.Count; i++)
            {
                TutorialStep s = steps[i];
                if (s == null || string.IsNullOrEmpty(s.after)) continue;
                if (!firstIndex.TryGetValue(s.after, out int target))
                    problems.Add($"{i}번 단계 '{s.key}'의 after '{s.after}'가 없는 key입니다");
                else if (target >= i)
                    problems.Add($"{i}번 단계 '{s.key}'의 after '{s.after}'가 자기 자신이거나 뒤({target}번)에 있습니다");
            }
            return problems;
        }
    }
}
