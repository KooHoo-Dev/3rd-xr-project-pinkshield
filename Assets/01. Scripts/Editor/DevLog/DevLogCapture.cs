using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace DevLogTools
{
    /// <summary>
    /// DevLog 캡처 도구. F9 = 스크린샷 + 버그 기록 창, Shift+F9 = 스크린샷만 _inbox/에 저장.
    /// 플레이 중에는 Game 뷰, 편집 중에는 Scene 뷰를 찍는다. 최근 경고/에러 로그를 모아 둔다.
    /// 결과물은 프로젝트 루트의 DevLog/ (Assets 밖)에 저장된다.
    /// </summary>
    [InitializeOnLoad]
    public static class DevLogCapture
    {
        const int MaxLogs = 10;
        static readonly Queue<string> logs = new Queue<string>();
        static double lastTrigger = -10;
        static bool prevF9;

        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string Root => Path.Combine(ProjectRoot, "DevLog");
        public static string ShotsDir => Path.Combine(Root, "screenshots");
        static string PendingDir => Path.Combine(ShotsDir, "_pending");
        public static string InboxDir => Path.Combine(ShotsDir, "_inbox");

        static DevLogCapture()
        {
            Application.logMessageReceived += OnLog;
            EditorApplication.update += PollKeys;
        }

        // ---------- 메뉴 / 단축키 ----------

        [MenuItem("DevLog/버그 기록 (스크린샷 포함) _F9", priority = 0)]
        public static void BugReport()
        {
            if (Debounce()) return;
            DevLogBugWindow.Open(Capture(PendingDir));
        }

        [MenuItem("DevLog/스크린샷만 저장 #F9", priority = 1)]
        public static void QuickShot()
        {
            if (Debounce()) return;
            var path = Capture(InboxDir);
            if (path != null) Debug.Log($"[DevLog] 스크린샷 저장: {Mask(path)}");
        }

        [MenuItem("DevLog/DevLog 폴더 열기", priority = 20)]
        static void OpenFolder() => EditorUtility.RevealInFinder(Path.Combine(Root, "INDEX.md"));

        // 플레이 중 Game 뷰에 포커스가 있으면 메뉴 단축키가 먹지 않을 수 있어 Input System으로도 감시한다.
        static void PollKeys()
        {
            if (!EditorApplication.isPlaying) { prevF9 = false; return; }
            var kb = Keyboard.current;
            if (kb == null) return;
            bool down = kb.f9Key.isPressed;
            if (down && !prevF9)
            {
                if (kb.shiftKey.isPressed) QuickShot();
                else BugReport();
            }
            prevF9 = down;
        }

        static bool Debounce()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - lastTrigger < 0.5) return true;
            lastTrigger = now;
            return false;
        }

        // ---------- 캡처 ----------

        /// <summary>캡처 파일 경로를 돌려준다. 플레이 중에는 프레임 끝에 파일이 써지므로 바로 존재하지 않을 수 있다.</summary>
        static string Capture(string dir)
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");

            if (EditorApplication.isPlaying)
            {
                ScreenCapture.CaptureScreenshot(path);
                return path;
            }

            var sv = SceneView.lastActiveSceneView;
            if (sv == null || sv.camera == null)
            {
                Debug.LogWarning("[DevLog] 캡처할 Scene 뷰가 없습니다. 스크린샷 없이 기록합니다.");
                return null;
            }

            var cam = sv.camera;
            int w = Mathf.Max(1, cam.pixelWidth), h = Mathf.Max(1, cam.pixelHeight);
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(tex);
            }
            return path;
        }

        // ---------- 로그 ----------

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Log) return;
            var at = stack?.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            var line = $"[{DateTime.Now:HH:mm:ss}] {type}: {msg}" + (string.IsNullOrEmpty(at) ? "" : $"\n    at {at}");
            logs.Enqueue(line);
            while (logs.Count > MaxLogs) logs.Dequeue();
        }

        public static string RecentLogs() => logs.Count == 0 ? "(경고·에러 없음)" : Mask(string.Join("\n", logs));

        // ---------- 공용 도구 ----------

        /// <summary>사용자 폴더 경로를 ~ 로 바꾼다 (공개 저장소 대비).</summary>
        public static string Mask(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home)) return text;
            return text.Replace(home, "~").Replace(home.Replace('\\', '/'), "~");
        }

        public static string Git(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    WorkingDirectory = ProjectRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };
                using var p = Process.Start(psi);
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);
                return output.Trim();
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static int NextNumber(string dir, string prefix)
        {
            Directory.CreateDirectory(dir);
            var re = new Regex("^" + prefix + @"-(\d+)");
            return Directory.GetFiles(dir, prefix + "-*.md")
                .Select(f => re.Match(Path.GetFileName(f)))
                .Where(m => m.Success)
                .Select(m => int.Parse(m.Groups[1].Value))
                .DefaultIfEmpty(0)
                .Max() + 1;
        }

        public static string Slug(string title)
        {
            var s = Regex.Replace(title.Trim(), @"[\\/:*?""<>|#\[\]()\s]+", "-").Trim('-');
            return s.Length > 40 ? s.Substring(0, 40).TrimEnd('-') : s;
        }

        /// <summary>INDEX.md의 마커 바로 아래에 행을 넣는다 (최신이 위).</summary>
        public static void InsertIndexRow(string marker, string row)
        {
            var index = Path.Combine(Root, "INDEX.md");
            var text = File.Exists(index) ? File.ReadAllText(index) : "";
            var tag = $"<!-- {marker} -->";
            int at = text.IndexOf(tag, StringComparison.Ordinal);
            text = at < 0
                ? text.TrimEnd() + "\n" + row + "\n"
                : text.Insert(at + tag.Length, "\n" + row);
            WriteUtf8(index, text);
        }

        public static void WriteUtf8(string path, string text) =>
            File.WriteAllText(path, text.Replace("\r\n", "\n"), new UTF8Encoding(false));
    }
}
