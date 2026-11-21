using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.OpenXR.Features;

namespace CardShop.EditorTools
{
    /// <summary>
    /// Meta Quest 빌드 설정과 APK 빌드. 메뉴: Tools > CardShop > Quest > ...
    /// 설정을 코드로 고정해 두면 실수로 바뀌어도 메뉴 한 번으로 되돌릴 수 있습니다.
    /// </summary>
    public static class QuestBuildTools
    {
        public const string ProductName = "슬라임 카드샵 VR";
        public const string ApplicationId = "com.KooHooEdu.onepackaftershift";
        public const string OutputDir = "Builds/Quest"; // .gitignore의 /Builds/ 에 포함
        const string ApkName = "CardKkangVR.apk";

        // Quest VR에 필요한 OpenXR 기능
        static readonly string[] RequiredFeatures =
        {
            "com.unity.openxr.feature.metaquest",
            "com.unity.openxr.feature.input.oculustouch",
            "com.unity.openxr.feature.input.metaquestplus",
        };

        // 이 게임에서 쓰지 않는 기능. 켜 두면 공간 데이터 권한 요청, 다른 기기용 매니페스트가 들어갑니다.
        static readonly string[] UnusedFeatures =
        {
            "com.unity.openxr.feature.androidxr-support",
            "com.unity.openxr.feature.arfoundation-meta-session",
            "com.unity.openxr.feature.arfoundation-meta-camera",
            "com.unity.openxr.feature.arfoundation-meta-plane",
            "com.unity.openxr.feature.arfoundation-meta-mesh",
            "com.unity.openxr.feature.arfoundation-meta-anchor",
            "com.unity.openxr.feature.arfoundation-meta-raycast",
            "com.unity.openxr.feature.arfoundation-meta-occlusion",
            "com.unity.openxr.feature.arfoundation-meta-bounding-boxes",
        };

        [MenuItem("Tools/CardShop/Quest/Apply Build Settings", priority = 50)]
        public static void ApplySettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            int changed = 0;
            foreach (string id in RequiredFeatures) changed += SetFeature(id, true);
            foreach (string id in UnusedFeatures) changed += SetFeature(id, false);

            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestBuildTools] Quest 설정 적용 완료 (OpenXR 기능 {changed}개 변경). Project Validation 경고도 확인하세요.");
        }

        static int SetFeature(string id, bool enabled)
        {
            OpenXRFeature feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, id);
            if (feature == null)
            {
                if (enabled) Debug.LogWarning($"[QuestBuildTools] OpenXR 기능을 찾을 수 없음: {id}");
                return 0;
            }
            if (feature.enabled == enabled) return 0;
            feature.enabled = enabled;
            EditorUtility.SetDirty(feature);
            return 1;
        }

        [MenuItem("Tools/CardShop/Quest/Build APK (Development)", priority = 51)]
        public static void BuildDevelopment() => Build(true, false);

        [MenuItem("Tools/CardShop/Quest/Build And Run (Development)", priority = 52)]
        public static void BuildAndRunDevelopment() => Build(true, true);

        [MenuItem("Tools/CardShop/Quest/Build APK (Release)", priority = 53)]
        public static void BuildRelease() => Build(false, false);

        /// <summary>배치 모드: -executeMethod CardShop.EditorTools.QuestBuildTools.BuildFromCommandLine</summary>
        public static void BuildFromCommandLine()
        {
            ApplySettings();
            BuildReport report = Build(true, false);
            EditorApplication.Exit(report != null && report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildReport Build(bool development, bool run)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[QuestBuildTools] Android 플랫폼으로 전환하지 못했습니다. Android Build Support 모듈을 확인하세요.");
                return null;
            }

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[QuestBuildTools] 빌드 씬이 없습니다. Shop.unity가 있는지 확인하세요 (처음 상태로 만들려면 Tools > CardShop > 초기화 > Rebuild Shop Scene).");
                return null;
            }

            Directory.CreateDirectory(OutputDir);
            BuildOptions options = BuildOptions.None;
            if (development) options |= BuildOptions.Development; // 디버그 치트(DEVELOPMENT_BUILD) 포함
            if (run) options |= BuildOptions.AutoRunPlayer;      // adb로 연결된 Quest에 설치·실행

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutputDir, ApkName),
                target = BuildTarget.Android,
                options = options,
            });

            BuildSummary s = report.summary;
            if (s.result == BuildResult.Succeeded)
                Debug.Log($"[QuestBuildTools] 빌드 성공: {s.outputPath} (APK {new FileInfo(s.outputPath).Length / (1024f * 1024f):0.0} MB, {s.totalTime.TotalSeconds:0}초)");
            else
                Debug.LogError($"[QuestBuildTools] 빌드 실패: {s.result}, 오류 {s.totalErrors}건");
            return report;
        }
    }
}
