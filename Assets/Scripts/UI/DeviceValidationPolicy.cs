using System;
using System.Collections.Generic;

namespace ShadowTheater.UI
{
    /// <summary>기기와 에디터가 동일하게 적용하는 최소 출시 기준.</summary>
    public static class DeviceValidationPolicy
    {
        public static List<string> Evaluate(DeviceValidationReport report)
        {
            var failures = new List<string>();
            if (report == null) { failures.Add("리포트 누락"); return failures; }
            if (!Finite(report.sessionSeconds) || report.sessionSeconds < 1200f) failures.Add("플레이 시간 20분 미만 또는 잘못된 값");
            if (!Finite(report.averageFps) || report.averageFps < 25f) failures.Add("평균 FPS 25 미만 또는 잘못된 값");
            if (report.sceneLoads < 5) failures.Add("씬 이동 5회 미만");
            if (report.battlesCompleted < 3) failures.Add("전투 완료 3회 미만");
            if (report.savesCompleted < 3) failures.Add("저장 3회 미만");
            if (report.pauseCount < 1 || report.resumeCount < 1) failures.Add("백그라운드 전환·복귀 미검증");
            if (report.lowMemoryEvents != 0) failures.Add("저메모리 이벤트 발생 또는 잘못된 값");
            if (report.errorCount != 0 || (report.errors != null && report.errors.Count > 0)) failures.Add("오류/예외 로그 발생 또는 잘못된 값");
            if (string.IsNullOrWhiteSpace(report.deviceModel)) failures.Add("기기 정보 누락");
            if (!DateTimeOffset.TryParse(report.generatedAtUtc, out _)) failures.Add("생성 시각 누락 또는 형식 오류");
            return failures;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
