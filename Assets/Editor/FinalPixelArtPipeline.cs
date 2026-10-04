#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowTheater.Data;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>최종 픽셀 아트를 규격대로 임포트하고 자동 생성 아트 대비 교체 진행률을 기록한다.</summary>
    public static class FinalPixelArtPipeline
    {
        public const string ReportPath = "Assets/Art/Final/FinalArtCoverage.json";
        private const string PortraitFolder = "Assets/Art/Final/Portraits";
        private const string FieldFolder = "Assets/Art/Final/Field";
        private const string PlayerFolder = "Assets/Art/Final/Player";
        private const string TileFolder = "Assets/Art/Final/Tiles";

        private static readonly string[] ThemeNames =
        {
            "Theater", "Village", "Boss", "AshWastes", "EmberCity", "Catacombs", "AshThrone",
            "FrostPort", "Archive", "ForbiddenStacks", "MirrorVault", "BlueAbyss", "VioletMarsh",
            "HowlVillage", "MoonfangForest", "BloodmoonRidge", "BeastDen", "ThreadMarket",
            "ClockworkAlley", "MarionetteOpera", "SeveredWorkshop", "PuppeteerStage", "Censor",
            "BlackArchive", "MemorySea", "MoonPalace", "FinalTheater", "CosmicStage"
        };

        // 초반 플레이에서 즉시 보이는 핵심 배역. 이 파일들은 저장소에 완성 아트로 포함한다.
        private static readonly string[] BundledCoreCast =
        {
            "knight", "mage", "beast", "crow", "puppet", "mask",
            "ash_hound", "banner_spearman", "soot_archer", "furnace_keeper"
        };

        [MenuItem("Tools/Shadow Theater/Import and Validate Final Pixel Art")]
        public static void ImportAndValidate()
        {
            EnsureFolders();
            var invalid = new List<string>();
            ImportFolder(PortraitFolder, 256f, FilterMode.Point, Vector2.one * .5f,
                (path, texture) =>
                {
                    if (texture.width != texture.height || texture.width < 192)
                        invalid.Add($"초상은 192px 이상 정사각형이어야 함: {path} ({texture.width}x{texture.height})");
                });
            ImportFolder(FieldFolder, 32f, FilterMode.Point, new Vector2(.5f, 0f),
                (path, texture) =>
                {
                    if (texture.width != 24 || texture.height != 32)
                        invalid.Add($"그림자 필드 프레임은 24x32여야 함: {path} ({texture.width}x{texture.height})");
                    string stem = Path.GetFileNameWithoutExtension(path);
                    if (!stem.EndsWith("_01", StringComparison.Ordinal) && !stem.EndsWith("_02", StringComparison.Ordinal))
                        invalid.Add("그림자 필드 파일은 _01 또는 _02로 끝나야 함: " + path);
                });
            ImportFolder(PlayerFolder, 32f, FilterMode.Point, new Vector2(.5f, 0f),
                (path, texture) =>
                {
                    if (texture.width != 24 || texture.height != 32)
                        invalid.Add($"주인공 필드 프레임은 24x32여야 함: {path} ({texture.width}x{texture.height})");
                });
            ImportFolder(TileFolder, 32f, FilterMode.Point, Vector2.one * .5f,
                (path, texture) =>
                {
                    if (texture.width != 32 || texture.height != 32)
                        invalid.Add($"필드 타일은 32x32여야 함: {path} ({texture.width}x{texture.height})");
                });

            ApplyPortraitOverrides();
            FinalArtCoverageReport report = BuildReport(invalid);
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            if (invalid.Count > 0)
                Debug.LogError("[FinalArt] 규격 오류\n- " + string.Join("\n- ", invalid));
            else
                Debug.Log($"[FinalArt] 임포트 완료: 초상 {report.portraitsReady}/{report.shadowCount}, " +
                          $"필드 {report.fieldActorsReady}/{report.shadowCount}, 플레이어 {report.playerFramesReady}/6, " +
                          $"타일 {report.tilesReady}/{report.tileCount}");
        }

        private static void ApplyPortraitOverrides()
        {
            var database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>("Assets/Resources/ShadowDatabase.asset");
            if (database?.shadows == null) return;
            foreach (ShadowData shadow in database.shadows)
            {
                if (shadow == null || string.IsNullOrEmpty(shadow.shadowId)) continue;
                string path = $"{PortraitFolder}/{shadow.shadowId}.png";
                if (!File.Exists(path)) continue;
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                shadow.silhouetteSprite = sprite;
                EditorUtility.SetDirty(shadow);
            }
        }

        private static FinalArtCoverageReport BuildReport(List<string> invalid)
        {
            var database = AssetDatabase.LoadAssetAtPath<ShadowDatabase>("Assets/Resources/ShadowDatabase.asset");
            string[] ids = database != null && database.shadows != null
                ? database.shadows.Where(x => x != null).Select(x => x.shadowId)
                    .Distinct().OrderBy(x => x).ToArray()
                : Array.Empty<string>();
            var knownIds = new HashSet<string>(ids, StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(PortraitFolder, "*.png"))
            {
                string id = Path.GetFileNameWithoutExtension(path);
                if (!knownIds.Contains(id)) invalid.Add("알 수 없는 그림자 초상 파일명: " + path.Replace('\\', '/'));
            }
            foreach (string path in Directory.GetFiles(FieldFolder, "*.png"))
            {
                string stem = Path.GetFileNameWithoutExtension(path);
                if (stem.Length < 4) continue;
                string id = stem.Substring(0, stem.Length - 3);
                if (!knownIds.Contains(id)) invalid.Add("알 수 없는 그림자 필드 파일명: " + path.Replace('\\', '/'));
            }
            int portraits = ids.Count(id => File.Exists($"{PortraitFolder}/{id}.png"));
            int actors = ids.Count(id => File.Exists($"{FieldFolder}/{id}_01.png") &&
                                         File.Exists($"{FieldFolder}/{id}_02.png"));
            string[] playerNames = { "down_01", "down_02", "up_01", "up_02", "side_01", "side_02" };
            var playerFiles = new HashSet<string>(playerNames.Select(x => "director_" + x), StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(PlayerFolder, "*.png"))
                if (!playerFiles.Contains(Path.GetFileNameWithoutExtension(path)))
                    invalid.Add("알 수 없는 주인공 프레임 파일명: " + path.Replace('\\', '/'));
            int player = playerNames.Count(name => File.Exists($"{PlayerFolder}/director_{name}.png"));
            foreach (string name in playerNames)
                if (!File.Exists($"{PlayerFolder}/director_{name}.png"))
                    invalid.Add("번들 주인공 프레임 누락: Player/director_" + name + ".png");
            foreach (string id in BundledCoreCast)
            {
                if (!File.Exists($"{PortraitFolder}/{id}.png"))
                    invalid.Add("번들 핵심 초상 누락: Portraits/" + id + ".png");
                if (!File.Exists($"{FieldFolder}/{id}_01.png") || !File.Exists($"{FieldFolder}/{id}_02.png"))
                    invalid.Add("번들 핵심 필드 프레임 누락: Field/" + id + "_01,_02.png");
            }
            string[] tiles = ExpectedTiles().ToArray();
            var tileFiles = new HashSet<string>(tiles, StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(TileFolder, "*.png"))
                if (!tileFiles.Contains(Path.GetFileNameWithoutExtension(path)))
                    invalid.Add("알 수 없는 타일 파일명: " + path.Replace('\\', '/'));
            int tileReady = tiles.Count(name => File.Exists($"{TileFolder}/{name}.png"));
            var missing = new List<string>();
            missing.AddRange(ids.Where(id => !File.Exists($"{PortraitFolder}/{id}.png"))
                .Take(12).Select(id => "Portraits/" + id + ".png"));
            missing.AddRange(ids.Where(id => !File.Exists($"{FieldFolder}/{id}_01.png") ||
                                             !File.Exists($"{FieldFolder}/{id}_02.png"))
                .Take(12).Select(id => "Field/" + id + "_01,_02.png"));
            missing.AddRange(playerNames.Where(name => !File.Exists($"{PlayerFolder}/director_{name}.png"))
                .Select(name => "Player/director_" + name + ".png"));
            missing.AddRange(tiles.Where(name => !File.Exists($"{TileFolder}/{name}.png"))
                .Take(12).Select(name => "Tiles/" + name + ".png"));
            return new FinalArtCoverageReport
            {
                shadowCount = ids.Length,
                portraitsReady = portraits, fieldActorsReady = actors, playerFramesReady = player,
                tileCount = tiles.Length, tilesReady = tileReady, invalid = invalid, missingExamples = missing
            };
        }

        private static IEnumerable<string> ExpectedTiles()
        {
            foreach (string theme in ThemeNames)
            {
                yield return theme + "_Ground";
                yield return theme + "_Wall";
                yield return theme + "_Accent";
            }
            yield return "MeadowPixel_Grass";
            yield return "MeadowPixel_Path";
            yield return "MeadowPixel_Water";
            yield return "MeadowPixel_Cliff";
            yield return "MeadowPixel_Bush";
        }

        private static void ImportFolder(string folder, float ppu, FilterMode filter, Vector2 pivot,
                                         Action<string, Texture2D> validate)
        {
            foreach (string path in Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly)
                         .Select(x => x.Replace('\\', '/')).OrderBy(x => x))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = ppu;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                importer.SetTextureSettings(settings);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = filter;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null) validate(path, texture);
            }
        }

        private static void EnsureFolders()
        {
            Ensure("Assets/Art", "Final");
            Ensure("Assets/Art/Final", "Portraits");
            Ensure("Assets/Art/Final", "Field");
            Ensure("Assets/Art/Final", "Player");
            Ensure("Assets/Art/Final", "Tiles");
        }

        private static void Ensure(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }
    }

    [Serializable]
    public class FinalArtCoverageReport
    {
        public int shadowCount, portraitsReady, fieldActorsReady, playerFramesReady, tileCount, tilesReady;
        public List<string> invalid = new List<string>();
        public List<string> missingExamples = new List<string>();
    }
}
#endif
