using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevLogTools
{
    /// <summary>버그 기록 창. 저장하면 DevLog/bugs/BUG-NNN-제목.md를 만들고 INDEX.md에 행을 넣는다.</summary>
    public class DevLogBugWindow : EditorWindow
    {
        static readonly string[] Severities = { "치명", "높음", "보통", "낮음" };
        static readonly string[] Envs = { "에디터 · XR Device Simulator", "에디터 · Quest Link", "Quest 실기기", "기타" };

        // 플레이 모드를 끝낼 때 도메인이 다시 로드돼도 입력이 남도록 전부 직렬화한다.
        [SerializeField] string bugTitle = "";
        [SerializeField] int severity = 2;
        [SerializeField] int env;
        [SerializeField] string expected = "";
        [SerializeField] string actual = "";
        [SerializeField] string steps = "1. \n2. \n3. ";
        [SerializeField] bool star;
        [SerializeField] List<string> shots = new List<string>();
        [SerializeField] bool initialized;
        [SerializeField] bool saved;
        [SerializeField] string found, scene, playTime, commit, logText;

        Texture2D preview;
        string previewPath;
        Vector2 scroll;

        public static void Open(string shot)
        {
            var w = GetWindow<DevLogBugWindow>(true, "DevLog · 버그 기록");
            if (!w.initialized) w.Init();
            if (!string.IsNullOrEmpty(shot)) w.shots.Add(shot);
            w.minSize = new Vector2(420, 560);
            w.Focus();
        }

        void Init()
        {
            initialized = true;
            found = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            scene = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(scene)) scene = "(저장 안 된 씬)";
            playTime = EditorApplication.isPlaying ? $"플레이 {Time.time:F1}s" : "편집 모드";

            var hash = DevLogCapture.Git("rev-parse --short HEAD");
            var branch = DevLogCapture.Git("rev-parse --abbrev-ref HEAD");
            var dirty = DevLogCapture.Git("status --porcelain").Length > 0 ? ", 미커밋 변경 있음" : "";
            commit = string.IsNullOrEmpty(hash) ? "(git 정보 없음)" : $"`{hash}` ({branch}{dirty})";
            logText = DevLogCapture.RecentLogs();
        }

        void OnInspectorUpdate() => Repaint(); // 플레이 중 캡처 파일이 써지면 미리보기를 갱신

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            bugTitle = EditorGUILayout.TextField("제목", bugTitle);
            severity = EditorGUILayout.Popup("심각도", severity, Severities);
            env = EditorGUILayout.Popup("환경", env, Envs);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("기대한 동작");
            expected = EditorGUILayout.TextArea(expected, GUILayout.MinHeight(36));
            EditorGUILayout.LabelField("실제 동작");
            actual = EditorGUILayout.TextArea(actual, GUILayout.MinHeight(36));
            EditorGUILayout.LabelField("재현 단계");
            steps = EditorGUILayout.TextArea(steps, GUILayout.MinHeight(54));
            star = EditorGUILayout.ToggleLeft("⭐ 포트폴리오 후보 (트러블슈팅에 쓸 만한 버그)", star);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"발견 {found} · 씬 {scene} · {playTime}\n커밋 {commit}\n\n최근 경고/에러:\n{logText}",
                MessageType.None);

            DrawShots();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("스크린샷 한 장 더 (F9)")) DevLogCapture.BugReport();
            GUI.enabled = bugTitle.Trim().Length > 0;
            if (GUILayout.Button("저장", GUILayout.Width(90))) Save();
            GUI.enabled = true;
            if (GUILayout.Button("취소", GUILayout.Width(60))) Close();
            EditorGUILayout.EndHorizontal();
        }

        void DrawShots()
        {
            EditorGUILayout.LabelField($"스크린샷 {shots.Count}장", EditorStyles.boldLabel);
            if (shots.Count == 0)
            {
                EditorGUILayout.LabelField("  (없음 — 저장 후 /devlog 또는 devlog-paste.ps1로 추가 가능)");
                return;
            }
            foreach (var s in shots)
                EditorGUILayout.LabelField("  " + Path.GetFileName(s) + (File.Exists(s) ? "" : "  (저장 중…)"));

            var last = shots[shots.Count - 1];
            if (last != previewPath && File.Exists(last))
            {
                if (preview != null) DestroyImmediate(preview);
                preview = new Texture2D(2, 2);
                try { preview.LoadImage(File.ReadAllBytes(last)); previewPath = last; }
                catch (IOException) { /* 아직 쓰는 중 — 다음 갱신 때 다시 */ }
            }
            if (preview != null)
            {
                float width = position.width - 30;
                float height = width * preview.height / Mathf.Max(1, preview.width);
                GUILayout.Label(preview, GUILayout.Width(width), GUILayout.Height(Mathf.Min(height, 240)));
            }
        }

        void Save()
        {
            var bugsDir = Path.Combine(DevLogCapture.Root, "bugs");
            int n = DevLogCapture.NextNumber(bugsDir, "BUG");
            var id = $"BUG-{n:000}";
            var file = $"{id}-{DevLogCapture.Slug(bugTitle)}.md";

            var images = MoveShots(id);
            var md = BuildMarkdown(id, images);
            DevLogCapture.WriteUtf8(Path.Combine(bugsDir, file), md);

            var t = bugTitle.Trim().Replace("|", "/");
            DevLogCapture.InsertIndexRow("bug-rows",
                $"| [{id}](bugs/{file}) | {t} | {Severities[severity]} | 열림 | {found.Substring(0, 10)} | - |");
            if (star)
                DevLogCapture.InsertIndexRow("star-rows", $"| [{id}](bugs/{file}) | 5-2 트러블슈팅 | {t} |");

            saved = true;
            Debug.Log($"[DevLog] {id} 저장: DevLog/bugs/{file}");
            Close();
        }

        List<string> MoveShots(string id)
        {
            var result = new List<string>();
            var dir = Path.Combine(DevLogCapture.ShotsDir, id);
            int i = 1;
            foreach (var s in shots.Where(File.Exists))
            {
                Directory.CreateDirectory(dir);
                var name = i == 1 ? "before.png" : $"before-{i}.png";
                File.Move(s, Path.Combine(dir, name));
                result.Add(name);
                i++;
            }
            shots.Clear();
            return result;
        }

        string BuildMarkdown(string id, List<string> images)
        {
            string Or(string v, string fallback) => string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();
            var sb = new StringBuilder();
            sb.AppendLine($"# {id} · {bugTitle.Trim()}");
            sb.AppendLine();
            sb.AppendLine("| 항목 | 값 |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| 상태 | 열림 |");
            sb.AppendLine($"| 심각도 | {Severities[severity]} |");
            sb.AppendLine($"| 발견 | {found} |");
            sb.AppendLine("| 해결 | - |");
            sb.AppendLine($"| 씬 / 환경 | {scene} · {Envs[env]} · {playTime} |");
            sb.AppendLine($"| 발견 커밋 | {commit} |");
            sb.AppendLine("| 해결 커밋 | - |");
            sb.AppendLine($"| 포트폴리오 | {(star ? "⭐" : "")} |");
            sb.AppendLine();
            sb.AppendLine("## 1. 증상 (문제)");
            sb.AppendLine($"- **기대**: {Or(expected, "(미작성)")}");
            sb.AppendLine($"- **실제**: {Or(actual, "(미작성)")}");
            sb.AppendLine();
            foreach (var img in images)
                sb.AppendLine($"![{Path.GetFileNameWithoutExtension(img)}](../screenshots/{id}/{img})");
            if (images.Count > 0) sb.AppendLine();
            sb.AppendLine("## 2. 재현 단계");
            sb.AppendLine(Or(steps, "(미작성)"));
            sb.AppendLine();
            sb.AppendLine("재현율: (미확인)");
            sb.AppendLine();
            sb.AppendLine("## 3. 콘솔 로그");
            sb.AppendLine("```text");
            sb.AppendLine(logText);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("## 4. 원인");
            sb.AppendLine("(미작성)");
            sb.AppendLine();
            sb.AppendLine("## 5. 해결");
            sb.AppendLine("(미작성)");
            sb.AppendLine();
            sb.AppendLine("## 6. 결과");
            sb.AppendLine("(미작성 — 고친 뒤 after 스크린샷: Shift+F9 후 /devlog bug " + id + ")");
            sb.AppendLine();
            sb.AppendLine("## 7. AI 협업");
            sb.AppendLine("- 내가 한 일:");
            sb.AppendLine("- AI가 제안한 것:");
            sb.AppendLine("- 받아들임 / 고침 / 거절 — 이유:");
            sb.AppendLine("- 관련 대화:");
            sb.AppendLine();
            sb.AppendLine("## 8. 배운 점");
            return DevLogCapture.Mask(sb.ToString());
        }

        void OnDestroy()
        {
            if (preview != null) DestroyImmediate(preview);
            if (saved) return;
            // 취소해도 캡처는 버리지 않고 _inbox/로 옮긴다.
            foreach (var s in shots.Where(File.Exists))
            {
                Directory.CreateDirectory(DevLogCapture.InboxDir);
                File.Move(s, Path.Combine(DevLogCapture.InboxDir, Path.GetFileName(s)));
            }
        }
    }
}
