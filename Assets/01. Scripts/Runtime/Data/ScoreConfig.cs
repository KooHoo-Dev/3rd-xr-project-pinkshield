using System;
using UnityEngine;

namespace CardShop.Cards
{
    /// <summary>최종 점수·랭크·엔딩 (기획서 5-8).</summary>
    [CreateAssetMenu(menuName = "CardShop/Config/Score", fileName = "ScoreConfig")]
    public class ScoreConfig : ScriptableObject
    {
        [Serializable]
        public class Rank
        {
            public string name;
            public int minScore;
            [Tooltip("이 랭크면 정직원 전환 엔딩")]
            public bool isSuccess;
        }

        [Tooltip("도감 1종당 점수")]
        public int pointsPerCollected = 10;

        [Tooltip("높은 랭크부터. 위에서부터 처음 만족하는 랭크가 선택됩니다.")]
        public Rank[] ranks =
        {
            new Rank { name = "S", minScore = 2800, isSuccess = true },
            new Rank { name = "A", minScore = 2200, isSuccess = true },
            new Rank { name = "B", minScore = 1500, isSuccess = true },
            new Rank { name = "C", minScore = 0, isSuccess = false },
        };

        public string successEnding = "정직원 전환";
        public string failEnding = "계약 연장";

        public Rank GetRank(int score)
        {
            foreach (Rank r in ranks)
                if (score >= r.minScore) return r;
            return ranks[ranks.Length - 1];
        }
    }
}
