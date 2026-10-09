#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>Android Studio Device Explorer/Xcode에서 꺼낸 QA JSON을 프로젝트 출시 증빙으로 가져온다.</summary>
    public static class DeviceValidationReportUtility
    {
        public const string ReportFolder = "Docs/DeviceValidationReports";
        public const string SummaryPath = "Docs/Generated/DEVICE_VALIDATION_SUMMARY.md";

        [MenuItem("Tools/Shadow Theater/Mobile/Import Device QA Report")]
        public static void Import()
        {
            string source = EditorUtility.OpenFilePanel("device_validation_latest.json 선택", string.Empty, "json");
            if (string.IsNullOrEmpty(source)) return;
            DeviceValidationReport report = Parse(source);
            if (report == null) { Debug.LogError("[DeviceValidation] 올바른 QA 리포트가 아닙니다."); return; }
            Directory.CreateDirectory(ReportFolder);
            string platform = Safe(report.platform);
            string device = Safe(report.deviceModel);
            string session = report.sessionId.Length > 8 ? report.sessionId.Substring(0, 8) : report.sessionId;
            string destination = Path.Combine(ReportFolder,
                $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{platform}_{device}_{session}.json");
            File.Copy(source, destination, true);
            WriteSummary();
            AssetDatabase.Refresh();
            Debug.Log($"[DeviceValidation] {(report.passed ? "PASS" : "FAIL")} 리포트 가져오기 완료: {destination}");
        }

        [MenuItem("Tools/Shadow Theater/Mobile/Validate Imported Device Reports")]
        public static void ValidateFromMenu()
        {
            var errors = new List<string>(); var warnings = new List<string>();
            ValidateImported(errors, warnings, true);
            WriteSummary();
            if (errors.Count == 0) Debug.Log("[DeviceValidation] Android/iOS 실기기 출시 게이트 통과");
            else Debug.LogError("[DeviceValidation] 출시 게이트 실패\n- " + string.Join("\n- ", errors));
            foreach (string warning in warnings) Debug.LogWarning("[DeviceValidation] " + warning);
        }

        public static void ValidateImported(List<string> errors, List<string> warnings, bool requireBothPlatforms)
        {
            var reports = LoadAll();
            if (reports.Count == 0)
            {
                (requireBothPlatforms ? errors : warnings).Add("가져온 실기기 QA 리포트가 없습니다");
                return;
            }
            var latest = reports.GroupBy(x => PlatformFamily(x.report.platform))
                .Select(x => x.OrderByDescending(y => ReportTime(y.report)).ThenByDescending(y => y.path).First())
                .ToList();
            foreach (ReportFile pair in latest)
            {
                if (pair.report.applicationVersion != MobileBuildConfigurator.Version)
                    errors.Add($"실기기 QA 앱 버전 불일치: {pair.report.platform}/{pair.report.applicationVersion} " +
                               $"(필요 {MobileBuildConfigurator.Version})");
                if (!pair.report.passed)
                    errors.Add($"최신 실기기 QA 실패: {pair.report.platform}/{pair.report.deviceModel} " +
                               $"({string.Join(", ", pair.report.failedCriteria ?? new List<string>())})");
            }
            if (!requireBothPlatforms) return;
            bool android = latest.Any(x => x.report.passed && x.report.applicationVersion == MobileBuildConfigurator.Version &&
                                           PlatformFamily(x.report.platform) == "Android");
            bool ios = latest.Any(x => x.report.passed && x.report.applicationVersion == MobileBuildConfigurator.Version &&
                                       PlatformFamily(x.report.platform) == "iOS");
            if (!android) errors.Add("통과한 Android 실기기 QA 리포트가 없습니다");
            if (!ios) errors.Add("통과한 iOS 실기기 QA 리포트가 없습니다");
        }

        private static List<ReportFile> LoadAll()
        {
            var result = new List<ReportFile>();
            if (!Directory.Exists(ReportFolder)) return result;
            foreach (string file in Directory.GetFiles(ReportFolder, "*.json", SearchOption.TopDirectoryOnly))
            {
                DeviceValidationReport report = Parse(file);
                if (report != null) result.Add(new ReportFile { path = file, report = report });
            }
            return result;
        }

        private static DeviceValidationReport Parse(string path)
        {
            try
            {
                var report = JsonUtility.FromJson<DeviceValidationReport>(File.ReadAllText(path));
                if (report == null || report.reportVersion != "2" || string.IsNullOrEmpty(report.sessionId)) return null;
                // JSON 내부의 passed 플래그를 신뢰하지 않고 실제 측정치로 재판정한다.
                report.failedCriteria = DeviceValidationPolicy.Evaluate(report);
                report.passed = report.failedCriteria.Count == 0;
                return report;
            }
            catch { return null; }
        }

        private static void WriteSummary()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SummaryPath));
            var reports = LoadAll();
            var text = new StringBuilder("# 실기기 QA 리포트\n\n");
            if (reports.Count == 0) text.AppendLine("가져온 리포트가 없습니다.");
            foreach (ReportFile item in reports.OrderByDescending(x => x.path))
            {
                DeviceValidationReport r = item.report;
                text.AppendLine($"## {(r.passed ? "PASS" : "FAIL")} · {r.platform} · {r.deviceModel}");
                text.AppendLine($"- OS: {r.operatingSystem}");
                text.AppendLine($"- 앱: {r.applicationVersion} / {(r.developmentBuild ? "QA" : "Release")}");
                text.AppendLine($"- 빌드: {r.buildGuid}");
                text.AppendLine($"- 시간: {r.sessionSeconds / 60f:F1}분 · 평균/최저 FPS: {r.averageFps:F1}/{r.minimumOneSecondFps:F1}");
                text.AppendLine($"- 씬/전투/저장: {r.sceneLoads}/{r.battlesCompleted}/{r.savesCompleted}");
                text.AppendLine($"- 백그라운드 복귀: {r.pauseCount}/{r.resumeCount} · 오류: {r.errorCount} · 저메모리: {r.lowMemoryEvents}");
                if (!r.passed) text.AppendLine("- 실패: " + string.Join(" · ", r.failedCriteria ?? new List<string>()));
                text.AppendLine();
            }
            File.WriteAllText(SummaryPath, text.ToString());
        }

        private static string Safe(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "unknown" : value;
            foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
            return safe.Replace(' ', '_');
        }

        private static string PlatformFamily(string platform)
        {
            if (!string.IsNullOrEmpty(platform) && platform.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Android";
            if (!string.IsNullOrEmpty(platform) && (platform.IndexOf("IPhone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                     platform.IndexOf("iOS", StringComparison.OrdinalIgnoreCase) >= 0))
                return "iOS";
            return string.IsNullOrEmpty(platform) ? "Unknown" : platform;
        }

        private static DateTime ReportTime(DeviceValidationReport report) =>
            DateTime.TryParse(report.generatedAtUtc, out DateTime value) ? value.ToUniversalTime() : DateTime.MinValue;

        private class ReportFile { public string path; public DeviceValidationReport report; }
    }
}
#endif
