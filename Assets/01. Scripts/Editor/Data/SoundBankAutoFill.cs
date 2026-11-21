using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CardShop.Cards;
using UnityEditor;
using UnityEngine;

namespace CardShop.EditorTools
{
    /// <summary>
    /// 파일 이름대로 소리 칸 채우기 (11-21). <c>Assets/04. Art/Sounds/&lt;칸 이름&gt;_&lt;숫자&gt;.wav · ogg · mp3</c>를
    /// SoundBank의 같은 이름 칸에 숫자 순서로 넣습니다. 예: CustomerComeMale_03.ogg → CustomerComeMale 칸 3번째.
    /// - 이름이 맞는 파일이 있는 칸: 그 파일들로 통째로 바꿈 (크기 · 음높이는 그대로)
    /// - 맞는 파일이 없는 칸: 그대로 두되, 지운 파일 자리(빈 연결)만 정리
    /// - 효과음(반복 칸 빼고)은 모노 · 48kHz 이하로 가져오기 설정을 맞춤 (Quest 메모리)
    /// 소리 파일을 넣거나 지우거나 이름을 바꾸면 자동으로 돌고, 메뉴로도 돌릴 수 있습니다.
    /// 등급 소리(RarityRevealProfile)는 SoundBank가 아니라서 여기서 채우지 않습니다.
    /// </summary>
    public sealed class SoundBankAutoFill : AssetPostprocessor
    {
        public const string Folder = "Assets/04. Art/Sounds";
        static readonly Regex NamePattern = new Regex(@"^(?<slot>.+?)(_(?<n>\d+))?$");
        static bool scheduled;

        [MenuItem("Tools/CardShop/Sound/파일 이름으로 칸 채우기", priority = 71)]
        static void RunFromMenu() => Run(true);

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (scheduled) return;
            if (!imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(IsSoundFile)) return;
            scheduled = true;
            EditorApplication.delayCall += () => { scheduled = false; Run(false); }; // 가져오기 도중에는 에셋을 건드리지 않음
        }

        static bool IsSoundFile(string path) =>
            path.StartsWith(Folder + "/") && Regex.IsMatch(path, @"\.(wav|ogg|mp3)$", RegexOptions.IgnoreCase);

        /// <param name="verbose">메뉴로 돌렸을 때: 바뀐 것이 없어도 결과와 칸에 안 맞는 파일을 알림</param>
        static void Run(bool verbose)
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(SoundBankCreator.Path);
            if (bank == null)
            {
                if (verbose) Debug.LogWarning($"[SoundBankAutoFill] {SoundBankCreator.Path} 없음 — Tools/CardShop/Sound/Create Sound Bank 먼저");
                return;
            }

            var bySlot = new Dictionary<SoundId, List<(int n, string name, AudioClip clip)>>();
            var unmatched = new List<string>();
            int reimported = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                Match m = NamePattern.Match(name);
                if (!Enum.TryParse(m.Groups["slot"].Value, false, out SoundId id) || !Enum.IsDefined(typeof(SoundId), id))
                {
                    unmatched.Add(name);
                    continue;
                }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                if (!SoundBank.IsLoop(id) && FixImport(path, clip)) { reimported++; continue; }
                int n = m.Groups["n"].Success ? int.Parse(m.Groups["n"].Value) : 0;
                if (!bySlot.TryGetValue(id, out var list)) bySlot[id] = list = new List<(int, string, AudioClip)>();
                list.Add((n, name, clip));
            }

            if (reimported > 0)
            {
                // 다시 가져오면 이 도구가 한 번 더 돌아서 칸을 채움 (반쯤 채운 칸을 남기지 않게 이번엔 멈춤)
                Debug.Log($"[SoundBankAutoFill] 효과음 {reimported}개 가져오기 설정 맞춤 (모노 · 44.1kHz) — 다시 가져온 뒤 칸을 채웁니다");
                return;
            }

            Undo.RecordObject(bank, "파일 이름으로 칸 채우기");
            int before = bank.entries.Count;
            int added = bank.EnsureAllEntries();
            bool dirty = added > 0 || bank.entries.Count - added != before;
            var log = new StringBuilder();
            foreach (SoundBank.Entry e in bank.entries)
            {
                AudioClip[] next = bySlot.TryGetValue(e.id, out var files)
                    ? files.OrderBy(f => f.n).ThenBy(f => f.name, StringComparer.Ordinal).Select(f => f.clip).ToArray()
                    : (e.clips ?? Array.Empty<AudioClip>()).Where(c => c != null).ToArray(); // 맞는 파일 없음: 끊긴 연결만 정리
                if (e.clips != null && e.clips.SequenceEqual(next)) continue;
                log.AppendLine($"  {e.id}: {Names(e.clips)} → {Names(next)}");
                e.clips = next;
                dirty = true;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(bank);
                AssetDatabase.SaveAssets();
                Debug.Log($"[SoundBankAutoFill] 소리 칸 갱신 (칸 추가 {added}, 없앤 칸 정리 {before + added - bank.entries.Count})\n{log}");
            }
            else if (verbose) Debug.Log("[SoundBankAutoFill] 바뀐 칸 없음");
            if (verbose && unmatched.Count > 0)
                Debug.Log($"[SoundBankAutoFill] 칸 이름과 맞지 않는 파일 {unmatched.Count}개 (그대로 둠): {string.Join(", ", unmatched)}");
        }

        /// <summary>효과음 가져오기 설정: 모노 · 48kHz 넘으면 44.1kHz. 바꿨으면 true (다시 가져옴).</summary>
        static bool FixImport(string path, AudioClip clip)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return false;
            bool changed = false;
            if (!importer.forceToMono) { importer.forceToMono = true; changed = true; }
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            if (clip.frequency > 48000 && s.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate)
            {
                s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                s.sampleRateOverride = 44100;
                importer.defaultSampleSettings = s;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
            return changed;
        }

        static string Names(AudioClip[] clips) =>
            clips == null || clips.Length == 0 ? "(비어 있음)" : string.Join(" ", clips.Select(c => c != null ? c.name : "(끊김)"));
    }
}
