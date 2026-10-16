#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    public static class ReleaseReadinessBatch
    {
        public static void ValidateAndCapture()
        {
            ReleaseVisualAssetGenerator.Generate();
            FinalPixelArtPipeline.ImportAndValidate();
            BattleUIPrefabGenerator.Generate();
            GameRegressionValidator.ValidateForCi();
            ReleasePreviewExporter.Capture();
        }

        public static void BuildAndroidQaForCi()
        {
            GameRegressionValidator.ValidateForCi();
            string output = "Builds/Android/ShadowTheater-ReleasePrep-QA.apk";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.CompressWithLz4HC
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Android QA build failed: {report.summary.result}, {report.summary.totalErrors} errors");
            Debug.Log($"[ReleaseQaBuild] PASS: {output} · {report.summary.totalSize} bytes · {report.summary.totalTime}");
        }
    }
}
#endif
