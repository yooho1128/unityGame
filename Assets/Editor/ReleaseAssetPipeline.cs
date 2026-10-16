#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>출시용 시각·음향 자산을 재생성하고 콘텐츠 참조 누락을 한 번에 감사한다.</summary>
    public static class ReleaseAssetPipeline
    {
        public const string ReportPath = "Assets/Reports/ReleaseAssetCoverage.json";
        private const string DatabasePath = "Assets/Resources/ShadowDatabase.asset";

        [MenuItem("Tools/Shadow Theater/Release/Generate and Audit Assets")]
        public static void GenerateAndAudit()
        {
            CoreContentBatchGenerator.Generate();
            LegendaryGrowthBatchGenerator.Generate();
            FinalPixelArtPipeline.ImportAndValidate();
            UltimateFxAssetGenerator.Generate();
            StandardSkillAudioAssetGenerator.Generate();
            FieldAudioAssetGenerator.Generate();
            FrontEndPrefabGenerator.Generate(); // BGM 생성 후 CoreSystems 참조까지 함께 갱신한다.
            ReleaseAssetCoverageReport report = Audit(true);
            if (report.errors.Count == 0)
                Debug.Log($"[ReleaseAssets] 통과: 그림자 {report.shadowsWithPortrait}/{report.shadowCount}, " +
                          $"스킬 음향 {report.skillsWithAudio}/{report.skillCount}, 음악 {report.musicReady}/{report.musicExpected}");
            else
                Debug.LogError("[ReleaseAssets] 실패\n- " + string.Join("\n- ", report.errors));
            foreach (string warning in report.warnings) Debug.LogWarning("[ReleaseAssets] " + warning);
        }

        [MenuItem("Tools/Shadow Theater/Release/Audit Assets Only")]
        public static void AuditFromMenu()
        {
            ReleaseAssetCoverageReport report = Audit(true);
            if (report.errors.Count == 0) Debug.Log("[ReleaseAssets] 자산 감사 통과");
            else Debug.LogError("[ReleaseAssets] 실패\n- " + string.Join("\n- ", report.errors));
            foreach (string warning in report.warnings) Debug.LogWarning("[ReleaseAssets] " + warning);
        }

        public static void Validate(List<string> errors, List<string> warnings)
        {
            ReleaseAssetCoverageReport report = Audit(false);
            errors.AddRange(report.errors.Select(x => "출시 자산: " + x));
            warnings.AddRange(report.warnings.Select(x => "출시 자산: " + x));
        }

        public static ReleaseAssetCoverageReport Audit(bool writeReport)
        {
            var report = new ReleaseAssetCoverageReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion
            };
            ShadowDatabase database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>(DatabasePath);
            if (database == null)
            {
                report.errors.Add("ShadowDatabase.asset 누락 — 전체 게임 생성을 먼저 실행하세요");
                return Finish(report, writeReport);
            }

            List<ShadowData> shadows = database.shadows.Where(x => x != null).ToList();
            List<SkillData> skills = database.skills.Where(x => x != null).ToList();
            report.shadowCount = shadows.Count;
            report.shadowsWithPortrait = shadows.Count(x => x.silhouetteSprite != null);
            report.finalPortraits = shadows.Count(x => File.Exists($"Assets/Art/Final/Portraits/{x.shadowId}.png"));
            report.finalFieldActors = shadows.Count(x =>
                File.Exists($"Assets/Art/Final/Field/{x.shadowId}_01.png") &&
                File.Exists($"Assets/Art/Final/Field/{x.shadowId}_02.png"));
            report.skillCount = skills.Count;
            report.skillsWithAudio = skills.Count(x => x.sfxClip != null && x.impactSfxClip != null);
            report.standardSkillCount = skills.Count(x => !x.isUltimate);
            report.standardSkillsWithAudio = skills.Count(x => !x.isUltimate && x.sfxClip != null && x.impactSfxClip != null);
            report.ultimateSkillCount = skills.Count(x => x.isUltimate);
            report.ultimatesWithFxAndAudio = skills.Count(x => x.isUltimate && x.fxPrefab != null &&
                                                             x.sfxClip != null && x.impactSfxClip != null);

            report.standardAudioExpected = Enum.GetValues(typeof(ShadowElement)).Length * 2;
            report.standardAudioReady = Enum.GetNames(typeof(ShadowElement)).Sum(name =>
                (AudioReady($"Assets/Audio/Generated/Skills/{name}_Cast.wav", report) ? 1 : 0) +
                (AudioReady($"Assets/Audio/Generated/Skills/{name}_Impact.wav", report) ? 1 : 0));
            report.musicExpected = Enum.GetValues(typeof(MusicCue)).Length;
            report.musicReady = Enum.GetNames(typeof(MusicCue)).Count(name =>
                AudioReady($"Assets/Audio/Generated/Music/{name}.wav", report));
            report.ambienceExpected = Enum.GetValues(typeof(FieldAmbienceStyle)).Length * 2;
            report.ambienceReady = Enum.GetNames(typeof(FieldAmbienceStyle)).Sum(name =>
                (AudioReady($"Assets/Audio/Generated/Field/Ambience/{name}_Bed.wav", report) ? 1 : 0) +
                (AudioReady($"Assets/Audio/Generated/Field/Ambience/{name}_Detail.wav", report) ? 1 : 0));
            report.footstepsExpected = Enum.GetValues(typeof(FootstepSurface)).Length * 4;
            report.footstepsReady = Enum.GetNames(typeof(FootstepSurface)).Sum(name =>
                Enumerable.Range(1, 4).Count(i =>
                    AudioReady($"Assets/Audio/Generated/Field/Footsteps/{name}_{i:00}.wav", report)));
            report.regionArrivalArtReady = RegionArrivalArtReady();

            if (report.shadowCount != 180) report.errors.Add($"그림자 데이터 수 {report.shadowCount}/180");
            if (report.shadowsWithPortrait != report.shadowCount)
                report.errors.Add($"전투 초상 참조 {report.shadowsWithPortrait}/{report.shadowCount}");
            if (report.skillsWithAudio != report.skillCount)
                report.errors.Add($"스킬 캐스트/타격음 {report.skillsWithAudio}/{report.skillCount}");
            if (report.ultimatesWithFxAndAudio != report.ultimateSkillCount)
                report.errors.Add($"필살기 FX·음향 {report.ultimatesWithFxAndAudio}/{report.ultimateSkillCount}");
            if (report.standardAudioReady != report.standardAudioExpected)
                report.errors.Add($"일반 스킬 WAV {report.standardAudioReady}/{report.standardAudioExpected}");
            if (report.musicReady != report.musicExpected)
                report.errors.Add($"BGM 파일 {report.musicReady}/{report.musicExpected}");
            if (report.ambienceReady != report.ambienceExpected)
                report.errors.Add($"환경음 파일 {report.ambienceReady}/{report.ambienceExpected}");
            if (report.footstepsReady != report.footstepsExpected)
                report.errors.Add($"발걸음 파일 {report.footstepsReady}/{report.footstepsExpected}");
            if (!report.regionArrivalArtReady)
                report.errors.Add("지역 진입 카드 배경·모티프 참조 누락");
            if (report.finalPortraits < report.shadowCount || report.finalFieldActors < report.shadowCount)
                report.warnings.Add($"수작업 최종 아트 교체율 — 초상 {report.finalPortraits}/{report.shadowCount}, " +
                                    $"필드 {report.finalFieldActors}/{report.shadowCount}; 나머지는 생성 실루엣 사용");
            return Finish(report, writeReport);
        }

        private static bool AudioReady(string path, ReleaseAssetCoverageReport report)
        {
            if (!File.Exists(path)) return false;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                report.errors.Add("오디오 임포트 실패: " + path);
                return false;
            }
            if (clip.channels != 1 || clip.length < .05f)
            {
                report.errors.Add($"오디오 규격 오류: {path} ({clip.channels}ch, {clip.length:0.000}s)");
                return false;
            }
            return true;
        }

        private static bool RegionArrivalArtReady()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/RegionArrivalCanvas.prefab");
            RegionArrivalBanner banner = prefab != null ? prefab.GetComponent<RegionArrivalBanner>() : null;
            if (banner == null) return false;
            var serialized = new SerializedObject(banner);
            return serialized.FindProperty("atmosphereGlow")?.objectReferenceValue != null &&
                   serialized.FindProperty("motifLeft")?.objectReferenceValue != null &&
                   serialized.FindProperty("motifRight")?.objectReferenceValue != null;
        }

        private static ReleaseAssetCoverageReport Finish(ReleaseAssetCoverageReport report, bool writeReport)
        {
            if (!writeReport) return report;
            Ensure("Assets", "Reports");
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            return report;
        }

        private static void Ensure(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }
    }

    [Serializable]
    public class ReleaseAssetCoverageReport
    {
        public string generatedAtUtc, unityVersion;
        public int shadowCount, shadowsWithPortrait, finalPortraits, finalFieldActors;
        public int skillCount, skillsWithAudio, standardSkillCount, standardSkillsWithAudio;
        public int ultimateSkillCount, ultimatesWithFxAndAudio;
        public int standardAudioExpected, standardAudioReady;
        public int musicExpected, musicReady, ambienceExpected, ambienceReady, footstepsExpected, footstepsReady;
        public bool regionArrivalArtReady;
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
    }
}
#endif
