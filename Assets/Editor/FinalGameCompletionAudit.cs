#if UNITY_EDITOR
using System;using System.Collections.Generic;using System.IO;using System.Text;using UnityEditor;using UnityEngine;
namespace ShadowTheater.EditorTools
{
    /// <summary>확장 콘텐츠가 아닌 현재 본편의 생성·진행·저장·모바일 출시 준비 상태를 한 문서로 고정한다.</summary>
    public static class FinalGameCompletionAudit
    {
        public const string ReportPath="Docs/Generated/FINAL_GAME_COMPLETION.md";

        [MenuItem("Tools/Shadow Theater/Completion/Generate Final Completion Report")]
        public static void Generate()
        {
            GameRegressionValidator.CompletionAuditSnapshot content=GameRegressionValidator.CollectCompletionAudit();
            var deviceErrors=new List<string>();var deviceWarnings=new List<string>();
            DeviceValidationReportUtility.ValidateImported(deviceErrors,deviceWarnings,true);
            var text=new StringBuilder();text.AppendLine("# 그림자 극장 본편 완성도 보고서");text.AppendLine();
            text.AppendLine($"- 생성 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            text.AppendLine($"- 본편 코드·데이터·씬 판정: {(content.Passed?"PASS":"BLOCKED")}");
            text.AppendLine($"- 모바일 실기기 출시 판정: {(deviceErrors.Count==0?"PASS":"BLOCKED")}");
            text.AppendLine($"- 규모: 지역 {content.regions}/63 · 생성 씬 {content.scenes}/63 · 그림자 {content.shadows}/180 · 포털 {content.portals} · 고정 조우 {content.encounters}");
            AppendGate(text,"1. 월드·빌드 구성",content.errors,"씬","Build Settings","포털","지역 수");
            AppendGate(text,"2. 모바일 UI·설정",content.errors,"안전영역","모바일","Android","iOS","화면 방향");
            AppendGate(text,"3. 스토리·퀘스트·엔딩",content.errors,"본편 완주","퀘스트","엔딩","대화","스토리");
            AppendGate(text,"4. 저장·진행 복구",content.errors,"세이브","마이그레이션","복구","v11","v12");
            AppendGate(text,"5. 전투·콘텐츠·아트",content.errors,"전투","그림자","밸런스","아트","음악","조우","출시 자산");
            AppendList(text,"전체 차단 항목",content.errors,"없음 — 현재 본편 자동 검증 통과");
            AppendList(text,"검토 경고",content.warnings,"없음");
            AppendList(text,"실기기 출시 차단 항목",deviceErrors,"없음 — Android/iOS 리포트 통과");
            AppendList(text,"실기기 경고",deviceWarnings,"없음");
            text.AppendLine("## 완료 기준");text.AppendLine();text.AppendLine("이 보고서의 본편 판정이 PASS이고 Android/iOS 실기기 판정까지 PASS인 커밋을 1.0 출시 후보로 사용한다. DLC는 이 기준 커밋 이후 별도 브랜치·버전에서 추가한다.");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));File.WriteAllText(ReportPath,text.ToString(),new UTF8Encoding(true));AssetDatabase.Refresh();
            if(content.Passed&&deviceErrors.Count==0)Debug.Log("[CompletionAudit] 본편·실기기 출시 준비 PASS · "+ReportPath);else Debug.LogWarning($"[CompletionAudit] 본편 차단 {content.errors.Count} · 실기기 차단 {deviceErrors.Count} · {ReportPath}");
        }

        [MenuItem("Tools/Shadow Theater/Completion/Generate Full Game and Audit")]
        public static void GenerateFullGameAndAudit(){MobileBuildConfigurator.Apply();PlayablePrologueGenerator.Generate();Generate();}

        private static void AppendGate(StringBuilder text,string title,List<string> errors,params string[] terms)
        {int count=0;foreach(string error in errors){foreach(string term in terms)if(error.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0){count++;break;}}text.AppendLine();text.AppendLine("## "+title);text.AppendLine();text.AppendLine(count==0?"PASS — 자동 검사 차단 항목 없음":$"BLOCKED — 관련 차단 항목 {count}개");}
        private static void AppendList(StringBuilder text,string title,List<string> values,string empty)
        {text.AppendLine();text.AppendLine("## "+title);text.AppendLine();if(values.Count==0){text.AppendLine("- "+empty);return;}foreach(string value in values)text.AppendLine("- "+value.Replace("\r"," ").Replace("\n"," "));}
    }
}
#endif
