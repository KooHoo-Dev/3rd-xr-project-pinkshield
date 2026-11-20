using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CardShop.Cards.EditorTools
{
    /// <summary>
    /// Assets/02. Data/cards.csv → CardData 에셋 생성·갱신 + CardDatabase 채우기.
    /// 메뉴: Tools > CardShop > Import Cards CSV
    /// 에셋 파일(GUID)은 유지하고 내용만 덮어쓰므로 프리팹 등의 참조가 깨지지 않습니다.
    /// </summary>
    public static class CardCsvImporter
    {
        public const string CsvPath = DataPaths.Root + "/cards.csv";

        [MenuItem("Tools/CardShop/Import Cards CSV", priority = 10)]
        public static void Import()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogError($"[CardCsvImporter] {CsvPath} 가 없습니다.");
                return;
            }

            ConfigAssetCreator.EnsureAll();
            var rarityTable = AssetDatabase.LoadAssetAtPath<RarityTable>(DataPaths.RarityTable);
            var database = AssetDatabase.LoadAssetAtPath<CardDatabase>(DataPaths.CardDatabase);

            List<string[]> rows = ParseCsv(File.ReadAllText(CsvPath, Encoding.UTF8));
            if (rows.Count < 2) { Debug.LogError("[CardCsvImporter] CSV에 데이터가 없습니다."); return; }

            var header = new Dictionary<string, int>();
            for (int i = 0; i < rows[0].Length; i++) header[rows[0][i].Trim()] = i;

            var imported = new List<CardData>();
            int warnings = 0;
            DataPaths.EnsureFolder(DataPaths.CardsDir);

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                string Get(string col) => header.TryGetValue(col, out int i) && i < row.Length ? row[i].Trim() : "";

                string id = Get("id");
                if (string.IsNullOrEmpty(id)) continue;

                if (!TryEnum(Get("kind"), out CardKind kind) ||
                    !TryEnum(Get("element"), out CardElement element) ||
                    !TryEnum(Get("rarity"), out CardRarity rarity))
                {
                    Debug.LogError($"[CardCsvImporter] {r + 1}행 {id}: kind/element/rarity 값을 읽을 수 없습니다.");
                    continue;
                }

                string path = $"{DataPaths.CardsDir}/{FileId(id)}.asset";
                var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (card == null)
                {
                    card = ScriptableObject.CreateInstance<CardData>();
                    AssetDatabase.CreateAsset(card, path);
                }

                card.id = id;
                card.number = imported.Count + 1; // 바인더 도감 번호와 같은 순서 (BinderCursor.NumberOf)
                card.kind = kind;
                card.element = element;
                card.rarity = rarity;
                card.displayName = Get("name");
                card.description = Get("description");
                card.basePrice = rarityTable.BasePrice(rarity);
                card.character = LoadArt(DataPaths.ArtChar, Get("char"), id, ref warnings);
                card.background = LoadArt(DataPaths.ArtBg, Get("bg"), id, ref warnings);
                card.foreground = LoadArt(DataPaths.ArtChar, Get("fg"), id, ref warnings);
                EditorUtility.SetDirty(card);
                imported.Add(card);
            }

            database.cards = imported;
            database.InvalidateCache();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CardCsvImporter] 카드 {imported.Count}장 임포트 완료. 아트 누락 {warnings}건 (아트가 준비되면 다시 임포트하세요).");
            DataValidator.Validate();
        }

        /// <summary>F-N1 → F_N1 (아트 가이드 7장 파일 이름 규칙)</summary>
        public static string FileId(string id) => id.Replace('-', '_');

        static Texture2D LoadArt(string dir, string name, string id, ref int missing)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{name}.png");
            if (tex == null) missing++;
            return tex;
        }

        static bool TryEnum<T>(string s, out T value) where T : struct => Enum.TryParse(s, true, out value);

        /// <summary>따옴표("...")와 줄바꿈이 든 칸을 지원하는 간단한 CSV 파서.</summary>
        public static List<string[]> ParseCsv(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else cell.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cell.ToString()); cell.Clear();
                    if (row.Count > 1 || row[0].Length > 0) rows.Add(row.ToArray());
                    row.Clear();
                }
                else if (c != '﻿') cell.Append(c);
            }
            row.Add(cell.ToString());
            if (row.Count > 1 || row[0].Length > 0) rows.Add(row.ToArray());
            return rows;
        }
    }
}
