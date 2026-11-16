using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CardShop.Cards.EditorTools
{
    /// <summary>
    /// 카드·설정 데이터 검사. 메뉴: Tools > CardShop > Validate Data
    /// 오류(Error)는 게임 규칙이 깨지는 문제, 경고(Warning)는 아트 누락처럼 진행 중인 작업입니다.
    /// </summary>
    public static class DataValidator
    {
        // 기획서 5-3: 등급별 종류 수 (노말 17 / 레어 12 / 수퍼레어 7 / 유니크 7 / 레전더리 7)
        static readonly int[] ExpectedPerRarity = { 17, 12, 7, 7, 7 };
        const float Epsilon = 0.01f;

        [MenuItem("Tools/CardShop/Validate Data", priority = 12)]
        public static bool Validate()
        {
            int errors = 0, warnings = 0;
            void Error(string msg) { errors++; Debug.LogError($"[DataValidator] {msg}"); }
            void Warn(string msg) { warnings++; Debug.LogWarning($"[DataValidator] {msg}"); }

            var db = AssetDatabase.LoadAssetAtPath<CardDatabase>(DataPaths.CardDatabase);
            var rarity = AssetDatabase.LoadAssetAtPath<RarityTable>(DataPaths.RarityTable);
            var odds = AssetDatabase.LoadAssetAtPath<PackOddsConfig>(DataPaths.PackOdds);
            var economy = AssetDatabase.LoadAssetAtPath<EconomyConfig>(DataPaths.Economy);
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfig>(DataPaths.Campaign);
            var score = AssetDatabase.LoadAssetAtPath<ScoreConfig>(DataPaths.Score);

            if (db == null) Error($"{DataPaths.CardDatabase} 없음 (Import Cards CSV 실행)");
            if (rarity == null) Error($"{DataPaths.RarityTable} 없음");
            if (odds == null) Error($"{DataPaths.PackOdds} 없음");
            if (economy == null) Error($"{DataPaths.Economy} 없음");
            if (campaign == null) Error($"{DataPaths.Campaign} 없음");
            if (score == null) Error($"{DataPaths.Score} 없음");

            // 카드
            if (db != null)
            {
                var ids = new HashSet<string>();
                var counts = new int[CardEnumUtil.RarityCount];
                var missingArt = new List<string>();
                foreach (CardData c in db.cards)
                {
                    if (c == null) { Error("CardDatabase에 빈 항목"); continue; }
                    if (string.IsNullOrEmpty(c.id)) { Error($"{c.name}: ID 없음"); continue; }
                    if (!ids.Add(c.id)) Error($"ID 중복: {c.id}");
                    counts[(int)c.rarity]++;

                    if (c.kind == CardKind.Slime && c.element == CardElement.None) Error($"{c.id}: 슬라임인데 속성이 None");
                    if (c.kind != CardKind.Slime && c.element != CardElement.None) Error($"{c.id}: 필드·간식인데 속성이 있음");
                    if (rarity != null && c.basePrice != rarity.BasePrice(c.rarity))
                        Error($"{c.id}: 기준가 {c.basePrice} ≠ RarityTable {rarity.BasePrice(c.rarity)} (CSV 다시 임포트)");

                    if ((c.kind != CardKind.Field && c.character == null) || c.background == null) missingArt.Add(c.id);
                }
                if (missingArt.Count > 0)
                    Warn($"아트 누락 {missingArt.Count}/{db.cards.Count}장: {string.Join(", ", missingArt)}");

                for (int r = 0; r < counts.Length; r++)
                    if (counts[r] != ExpectedPerRarity[r])
                        Error($"{(CardRarity)r} 카드 {counts[r]}종 (기획서 {ExpectedPerRarity[r]}종)");
            }

            // 등급 테이블
            if (rarity != null && rarity.entries.Length != CardEnumUtil.RarityCount)
                Error($"RarityTable 항목 {rarity.entries.Length}개 (5개 필요)");

            // 팩 확률
            if (odds != null)
            {
                CheckSum("일반 슬롯", odds.commonSlot);
                CheckSum("레어 슬롯", odds.rareSlot);
                CheckSum("히트 슬롯", odds.hitSlot);
                if (odds.hitSlot.normal > 0f) Warn("히트 슬롯에 노말 확률이 있음 (기획서: 모든 팩에 레어 이상 1장 보장)");
                float shelf = odds.shelfNoClue + odds.shelfOneClue + odds.shelfTwoClues;
                if (Mathf.Abs(shelf - 100f) > Epsilon) Error($"선반 단서 비율 합 {shelf}% (100% 필요)");
                if (odds.clueMultipliers.Length < 3) Error("단서 배율은 0·1·2개용 3칸 필요");
                for (int n = 0; n < odds.clueMultipliers.Length; n++)
                    foreach (PackSlotType slot in odds.clueBoostedSlots)
                    {
                        RarityWeights w = odds.GetSlotWeights(slot, n);
                        if (w.normal < 0f || w.rare < 0f) Error($"단서 {n}개 {slot} 슬롯: 배율이 너무 커서 확률이 음수");
                    }
            }

            void CheckSum(string label, RarityWeights w)
            {
                if (Mathf.Abs(w.Sum - 100f) > Epsilon) Error($"{label} 확률 합 {w.Sum}% (100% 필요)");
            }

            // 진행·점수
            if (campaign != null)
            {
                if (campaign.DayCount == 0) Error("CampaignConfig 일차가 0일");
                for (int d = 0; d < campaign.days.Count; d++)
                    if (campaign.days[d].customerMin > campaign.days[d].customerMax)
                        Error($"{d + 1}일차 손님 수 최소 > 최대");
            }
            if (score != null)
                for (int i = 1; i < score.ranks.Length; i++)
                    if (score.ranks[i].minScore > score.ranks[i - 1].minScore)
                        Error("랭크는 높은 점수 순으로 정렬되어야 함");

            string summary = $"[DataValidator] 오류 {errors}건, 경고 {warnings}건";
            if (errors > 0) Debug.LogError(summary); else Debug.Log(summary);
            return errors == 0;
        }
    }
}
