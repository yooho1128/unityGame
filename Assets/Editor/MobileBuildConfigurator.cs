#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>Android/iOS 공통 출시 설정과 재현 가능한 QA/릴리스 빌드를 한곳에서 관리한다.</summary>
    public static class MobileBuildConfigurator
    {
        public const string ApplicationId = "com.yooho.shadowtheater";
        public const string Version = "0.1.0";
        public const int AndroidVersionCode = 1;
        public const string IosBuildNumber = "1";
        private const string BuildRoot = "Builds";
        private const string TitleScenePath = "Assets/Scenes/Prologue/Title.unity";

        [MenuItem("Tools/Shadow Theater/Mobile/Configure Android and iOS")]
        public static void Apply()
        {
            PlayerSettings.productName = "Shadow Theater";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.runInBackground = false;

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, ApplicationId);
            PlayerSettings.Android.bundleVersionCode = AndroidVersionCode;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.resizableWindow = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard_2_0);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, ApplicationId);
            PlayerSettings.iOS.buildNumber = IosBuildNumber;
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.iOS, ApiCompatibilityLevel.NET_Standard_2_0);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Medium);

            AssetDatabase.SaveAssets();
            Debug.Log($"[MobileBuild] 설정 완료: {ApplicationId} v{Version} · Android API 26+/ARM64 · iOS 13+");
        }

        public static void ValidateSettings(List<string> errors)
        {
            if (errors == null) return;
            if (PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android) != ApplicationId)
                errors.Add("Android Application Identifier 불일치");
            if (PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS) != ApplicationId)
                errors.Add("iOS Bundle Identifier 불일치");
            if (PlayerSettings.bundleVersion != Version)
                errors.Add($"앱 버전 불일치: {PlayerSettings.bundleVersion}/{Version}");
            if (PlayerSettings.Android.bundleVersionCode < 1)
                errors.Add("Android Version Code는 1 이상이어야 합니다");
            if (string.IsNullOrWhiteSpace(PlayerSettings.iOS.buildNumber))
                errors.Add("iOS Build Number가 비어 있습니다");
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
                errors.Add("Color Space는 Linear여야 합니다");
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation ||
                PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown ||
                !PlayerSettings.allowedAutorotateToLandscapeLeft || !PlayerSettings.allowedAutorotateToLandscapeRight)
                errors.Add("모바일 화면 방향은 가로 자동 회전 전용이어야 합니다");
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel26)
                errors.Add("Android 최소 API는 26 이상이어야 합니다");
            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                errors.Add("Android ARM64 아키텍처가 비활성화되어 있습니다");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP ||
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS) != ScriptingImplementation.IL2CPP)
                errors.Add("Android/iOS Scripting Backend는 IL2CPP여야 합니다");
            ValidateScenePaths(EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToList(), errors);
        }

        public static void ValidateScenePolicyRegression(List<string> errors)
        {
            var valid = new List<string> { TitleScenePath };
            for (int i = 0; i < 40; i++) valid.Add($"Assets/Scenes/Prologue/Field{i:00}.unity");
            var validErrors = new List<string>();
            ValidateScenePaths(valid, validErrors);
            if (validErrors.Count != 0) errors.Add("모바일 빌드 씬 정책: 정상 목록이 실패함");
            var invalid = new List<string>(valid) { valid[1] };
            invalid[0] = valid[1];
            var invalidErrors = new List<string>();
            ValidateScenePaths(invalid, invalidErrors);
            if (invalidErrors.Count < 3) errors.Add("모바일 빌드 씬 정책: 수·첫 씬·중복 오류 검출 실패");
        }

        private static void ValidateScenePaths(IReadOnlyList<string> scenes, List<string> errors)
        {
            if (scenes == null || scenes.Count != 41)
                errors.Add($"Build Settings 활성 씬 수 불일치: {scenes?.Count ?? 0}/41");
            if (scenes == null || scenes.Count == 0 || scenes[0] != TitleScenePath)
                errors.Add("Build Settings 첫 씬은 Title이어야 합니다");
            if (scenes != null && scenes.Any(string.IsNullOrWhiteSpace))
                errors.Add("Build Settings에 빈 씬 경로가 있습니다");
            if (scenes != null && scenes.Distinct(StringComparer.Ordinal).Count() != scenes.Count)
                errors.Add("Build Settings 활성 씬 경로가 중복되었습니다");
        }

        [MenuItem("Tools/Shadow Theater/Mobile/Build Android QA APK")]
        public static void BuildAndroidQa()
        {
            Apply();
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, Path.Combine(BuildRoot, "Android", "ShadowTheater-QA.apk"),
                BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.CompressWithLz4HC);
        }

        [MenuItem("Tools/Shadow Theater/Mobile/Build Android Release AAB")]
        public static void BuildAndroidRelease()
        {
            Apply();
            if (!PlayerSettings.Android.useCustomKeystore)
            {
                EditorUtility.DisplayDialog("Android 서명 필요",
                    "Player Settings에서 업로드 키스토어를 연결한 뒤 다시 실행하세요. 키 비밀번호는 저장소에 커밋하지 않습니다.", "확인");
                return;
            }
            if (!ConfirmReleaseGate()) return;
            EditorUserBuildSettings.buildAppBundle = true;
            Build(BuildTarget.Android, Path.Combine(BuildRoot, "Android", "ShadowTheater.aab"),
                BuildOptions.CompressWithLz4HC);
        }

        [MenuItem("Tools/Shadow Theater/Mobile/Build iOS QA Xcode")]
        public static void BuildIosQa()
        {
            Apply();
            Build(BuildTarget.iOS, Path.Combine(BuildRoot, "iOS-QA"),
                BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.CompressWithLz4HC);
        }

        [MenuItem("Tools/Shadow Theater/Mobile/Build iOS Release Xcode")]
        public static void BuildIosRelease()
        {
            Apply();
            if (!ConfirmReleaseGate()) return;
            Build(BuildTarget.iOS, Path.Combine(BuildRoot, "iOS"), BuildOptions.CompressWithLz4HC);
        }

        private static bool ConfirmReleaseGate()
        {
            if (GameRegressionValidator.TryValidateReleaseGate(out string errors)) return true;
            EditorUtility.DisplayDialog("출시 게이트 실패",
                "Release 빌드를 중단했습니다. Console과 생성된 검증 보고서를 확인하세요.\n\n- " + errors,
                "확인");
            Debug.LogError("[MobileBuild] Release 빌드 차단\n- " + errors);
            return false;
        }

        private static void Build(BuildTarget target, string output, BuildOptions options)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray();
            var sceneErrors = new List<string>();
            ValidateScenePaths(scenes, sceneErrors);
            if (sceneErrors.Count > 0)
            {
                Debug.LogError("[MobileBuild] 씬 구성 오류\n- " + string.Join("\n- ", sceneErrors));
                return;
            }
            string directory = target == BuildTarget.iOS ? output : Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                options = options
            });
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"[MobileBuild] 성공: {output} · {summary.totalSize / (1024f * 1024f):F1} MB · {summary.totalTime}");
            else
                Debug.LogError($"[MobileBuild] 실패: {summary.result} · 오류 {summary.totalErrors}개");
        }
    }
}
#endif
