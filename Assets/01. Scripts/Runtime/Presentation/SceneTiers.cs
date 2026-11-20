using System.Collections.Generic;
using System.Text.RegularExpressions;
using CardShop.Cards;
using UnityEngine;

namespace CardShop.Presentation
{
    /// <summary>
    /// 씬에 직접 놓은 업그레이드 단계 가구 (11-20 플레이 F5): 집 실내 아래 "분야_Tier_번호"(예: Bed_Tier_0 · 1 · 2)가 있으면
    /// 그 분야는 카탈로그 모델 대신 단계에 맞는 것 하나만 켭니다 (1단계 = Tier_0). 모델 · 자리 · 크기는 씬에서 정합니다.
    /// 그 단계의 Tier가 없으면 그 아래에서 가장 높은 것 (예: Tier_0 · 2만 있으면 2단계는 Tier_0).
    /// </summary>
    public static class SceneTiers
    {
        static readonly Regex Pattern = new Regex(@"^(\w+)_Tier_(\d+)$");

        public static string Name(UpgradeKind kind, int tier) => $"{kind}_Tier_{tier}";

        /// <summary>있는 Tier 번호들 중 level 단계에 켤 번호 (없으면 -1). 순수 규칙 (SceneTiersTests)</summary>
        public static int Pick(ICollection<int> tiers, int level)
        {
            int want = level - 1, best = -1, lowest = int.MaxValue;
            foreach (int t in tiers)
            {
                if (t <= want && t > best) best = t;
                if (t < lowest) lowest = t;
            }
            return best >= 0 ? best : tiers.Count > 0 ? lowest : -1;
        }

        /// <summary>집 실내에서 그 분야의 Tier들 (꺼진 것 포함)</summary>
        public static Dictionary<int, Transform> Find(Transform interior, UpgradeKind kind)
        {
            var found = new Dictionary<int, Transform>();
            if (interior == null) return found;
            string prefix = kind.ToString();
            foreach (Transform t in interior.GetComponentsInChildren<Transform>(true))
            {
                Match m = Pattern.Match(t.name);
                if (m.Success && m.Groups[1].Value == prefix && int.TryParse(m.Groups[2].Value, out int n) && !found.ContainsKey(n))
                    found[n] = t;
            }
            return found;
        }

        /// <summary>단계에 맞는 Tier 하나만 켭니다. 그 분야에 Tier가 하나도 없으면 false (카탈로그 모델을 쓰라는 뜻).</summary>
        public static bool Apply(Transform interior, UpgradeKind kind, int level)
        {
            Dictionary<int, Transform> tiers = Find(interior, kind);
            if (tiers.Count == 0) return false;
            int on = Pick(tiers.Keys, level);
            foreach (KeyValuePair<int, Transform> e in tiers) e.Value.gameObject.SetActive(e.Key == on);
            return true;
        }
    }
}
