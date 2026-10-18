#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Field;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
namespace ShadowTheater.EditorTools
{
    public static class ShadowRosterArtBatch
    {
        public const string ReportPath = "Docs/Generated/ShadowRoster/ROSTER_ART.json";
        [Serializable] private class Entry { public string id, name, path, sprite; public int width, height; }
        [Serializable] private class Report { public int total, portraits, fieldSpecies, uniqueImages; public List<Entry> entries = new List<Entry>(); }
        public static void GenerateCheckAndBuildForCi()
        {
            MobileBuildConfigurator.Apply();
            PlayablePrologueGenerator.Generate();
            CheckAndBuildForCi();
        }
        public static void RebindCheckAndBuildForCi()
        {
            MobileBuildConfigurator.Apply();
            FinalPixelArtPipeline.ImportAndValidate();
            CheckAndBuildForCi();
        }
        private static void CheckAndBuildForCi()
        {
            CreateRegistry();
            var errors = new List<string>(); Validate(errors, true);
            if (errors.Count > 0) throw new BuildFailedException(string.Join("\n", errors));
            GameRegressionValidator.ValidateForCi();
            MobilePlayabilityChecks.CheckAndCaptureForCi();
            ReleaseReadinessBatch.BuildAndroidQaForCi();
        }
        public static void Validate(List<string> errors) => Validate(errors, false);
        private static void CreateRegistry()
        {
            const string path = "Assets/Resources/ShadowArtRoster.asset";
            var roster = AssetDatabase.LoadAssetAtPath<ShadowArtRoster>(path);
            if (roster == null) { roster = ScriptableObject.CreateInstance<ShadowArtRoster>(); AssetDatabase.CreateAsset(roster, path); }
            var database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>("Assets/Resources/ShadowDatabase.asset");
            var ids = database.shadows.Where(x => x != null).Select(x => x.shadowId)
                .Concat(ShadowPortraitAtlasImporter.ReadManifest().sheets.SelectMany(x => x.ids)).Distinct();
            roster.entries = ids.Select(id => new ShadowArtRoster.Binding
                { shadowId = id, portrait = FinalPixelArtPipeline.PortraitFor(id) }).ToList();
            EditorUtility.SetDirty(roster); AssetDatabase.SaveAssets();
        }
        private static void Validate(List<string> errors, bool writeReport)
        {
            var database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>("Assets/Resources/ShadowDatabase.asset");
            if (database == null) { errors.Add("그림자 전용 아트 검사: 데이터베이스 없음"); return; }
            var report = new Report();
            var registry = AssetDatabase.LoadAssetAtPath<ShadowArtRoster>("Assets/Resources/ShadowArtRoster.asset");
            if (registry == null || registry.entries.Count < 180 || registry.entries.Select(x => x.shadowId).Distinct().Count() != registry.entries.Count)
                errors.Add("기존 씬용 출시 아트 레지스트리 부족/중복");
            var images = new HashSet<string>(StringComparer.Ordinal);
            var knownIds = new HashSet<string>(database.shadows.Where(x => x != null).Select(x => x.shadowId));
            var reserved = new HashSet<string>(ShadowPortraitAtlasImporter.ReadManifest().reserveIds ?? Array.Empty<string>());
            if (registry != null && registry.entries.Count(x => knownIds.Contains(x.shadowId)) != 180)
                errors.Add("실제 게임 180종의 출시 아트 레지스트리 부족");
            foreach (var sheet in ShadowPortraitAtlasImporter.ReadManifest().sheets)
                foreach (string id in sheet.ids ?? Array.Empty<string>())
                    if (!knownIds.Contains(id) && !reserved.Contains(id)) errors.Add("전용 시트의 알 수 없는 ID: " + id);
            foreach (var shadow in database.shadows.Where(x => x != null))
            {
                report.total++;
                Sprite portrait = FinalPixelArtPipeline.PortraitFor(shadow.shadowId);
                if (portrait == null || shadow.silhouetteSprite != portrait)
                { errors.Add("종별 전용 전투/각본집 이미지 미연결: " + shadow.shadowId); continue; }
                report.portraits++;
                if (registry != null && registry.Portrait(shadow.shadowId) != portrait)
                    errors.Add("기존 씬용 출시 아트 레지스트리 이미지 불일치: " + shadow.shadowId);
                string path = AssetDatabase.GetAssetPath(portrait);
                if (!images.Add(path + ":" + portrait.name)) errors.Add("종별 이미지 영역 중복: " + shadow.shadowId);
                if (portrait.rect.width < 192 || portrait.rect.height < 192) errors.Add("종별 이미지 해상도 부족: " + shadow.shadowId);
                if (!FinalPixelArtPipeline.HasDedicatedFieldArt(shadow)) errors.Add("종별 전용 필드 이미지 미연결: " + shadow.shadowId);
                else report.fieldSpecies++;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var android = importer?.GetPlatformTextureSettings("Android");
                if (importer == null || !importer.alphaIsTransparency || importer.mipmapEnabled ||
                    android == null || !android.overridden || android.format != TextureImporterFormat.ETC2_RGBA8)
                    errors.Add("종별 이미지 Android 알파/압축 설정 오류: " + shadow.shadowId);
                report.entries.Add(new Entry { id = shadow.shadowId, name = shadow.displayName, path = path, sprite = portrait.name,
                    width = (int)portrait.rect.width, height = (int)portrait.rect.height });
                var preview = new GameObject("SpeciesSizing", typeof(SpriteRenderer));
                try
                {
                    var renderer = preview.GetComponent<SpriteRenderer>();
                    ShadowArtRoster.ApplyFieldVisual(renderer, shadow);
                    if (renderer.sprite != portrait) errors.Add("기존 씬 필드 이미지 갱신 실패: " + shadow.shadowId);
                    FieldSpriteSizing.Fit(renderer, .92f);
                    if (renderer.bounds.size.y > .93f || renderer.bounds.size.x > 1.16f)
                        errors.Add("종별 필드 이미지 한 칸 크기 초과: " + shadow.shadowId);
                }
                finally { UnityEngine.Object.DestroyImmediate(preview); }
            }
            report.uniqueImages = images.Count;
            if (report.total != 180 || report.portraits != 180 || report.fieldSpecies != 180 || images.Count != 180)
                errors.Add($"전용 아트 180종 미달: 데이터 {report.total}, 초상 {report.portraits}, 필드 {report.fieldSpecies}, 고유영역 {images.Count}");
            if (writeReport)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            }
            if (errors.Count == 0) Debug.Log("[ShadowRosterArt] PASS: 180/180 unique portraits, field species, alpha/mobile compression/one-tile bounds");
        }
    }
}
#endif
