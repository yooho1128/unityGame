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
    /// <summary>모바일과 같은 콘텐츠/저장을 사용하는 Windows 64-bit 빌드 설정.</summary>
    public static class StandaloneBuildConfigurator
    {
        public const string ApplicationId = "com.yooho.shadowtheater";
        private const string TitleScenePath = "Assets/Scenes/Prologue/Title.unity";
        private const int ExpectedBuildSceneCount = 64;

        [MenuItem("Tools/Shadow Theater/PC/Configure Windows")]
        public static void ApplyWindows()
        {
            PlayerSettings.productName = "Shadow Theater";
            PlayerSettings.bundleVersion = MobileBuildConfigurator.Version;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, ApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);
            AssetDatabase.SaveAssets();
            Debug.Log("[PCBuild] Windows 설정 완료: 64-bit · 1920×1080 · 창/전체 화면 전환 · 키보드/마우스/게임패드");
        }

        public static void ValidateScenePolicyRegression(List<string> errors)
        {
            var scenes = new List<string> { TitleScenePath };
            for (int i = 0; i < ExpectedBuildSceneCount - 1; i++)
                scenes.Add($"Assets/Scenes/Prologue/Field{i:00}.unity");
            ValidateScenePaths(scenes, errors, "PC 정상 목록");
            scenes.RemoveAt(scenes.Count - 1);
            var invalid = new List<string>();
            ValidateScenePaths(scenes, invalid, "PC 오류 목록");
            if (invalid.Count == 0) errors.Add("PC 빌드 씬 정책: 누락된 씬을 검출하지 못함");
        }

        [MenuItem("Tools/Shadow Theater/PC/Build Windows 64-bit")]
        public static void BuildWindows64()
        {
            ApplyWindows();
            string[] scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray();
            var errors = new List<string>();
            ValidateScenePaths(scenes, errors, "Windows 빌드");
            if (errors.Count > 0)
            {
                Debug.LogError("[PCBuild] 씬 구성 오류\n- " + string.Join("\n- ", errors));
                return;
            }

            string output = Path.Combine("Builds", "Windows", "ShadowTheater.exe");
            string directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC
            });
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"[PCBuild] 성공: {output} · {summary.totalSize / (1024f * 1024f):F1} MB");
            else
                Debug.LogError($"[PCBuild] 실패: {summary.result} · 오류 {summary.totalErrors}개");
        }

        private static void ValidateScenePaths(IReadOnlyList<string> scenes, List<string> errors, string label)
        {
            if (scenes == null || scenes.Count != ExpectedBuildSceneCount)
                errors.Add($"{label}: 활성 씬 수 불일치 ({scenes?.Count ?? 0}/{ExpectedBuildSceneCount})");
            if (scenes == null || scenes.Count == 0 || scenes[0] != TitleScenePath)
                errors.Add($"{label}: 첫 씬은 Title이어야 함");
            if (scenes != null && scenes.Any(string.IsNullOrWhiteSpace)) errors.Add($"{label}: 빈 씬 경로");
            if (scenes != null && scenes.Distinct(StringComparer.Ordinal).Count() != scenes.Count)
                errors.Add($"{label}: 중복 씬 경로");
        }
    }
}
#endif
