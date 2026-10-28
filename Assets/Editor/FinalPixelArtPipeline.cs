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
            ImportFolder(PortraitFolder, 256f, FilterMode.Point, new Vector2(.5f, 0),
                (path, texture) =>
                {
                    if (texture.width != texture.height || texture.width < 192)
                        invalid.Add($"초상은 192px 이상 정사각형이어야 함: {path} ({texture.width}x{texture.height})");
                });
            ImportFolder("Assets/Resources/Art/Evolution/Portraits", 256f, FilterMode.Point, new Vector2(.5f, 0f),
                (path, texture) => { if (texture.width != 512 || texture.height != 512) invalid.Add("진화 초상 규격: " + path); });
            ImportFolder("Assets/Resources/Art/Evolution/Field", 32f, FilterMode.Point, new Vector2(.5f, 0f),
                (path, texture) => { if (texture.width != 24 || texture.height != 32) invalid.Add("진화 동행 규격: " + path); });
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

            ShadowPortraitAtlasImporter.Import(invalid);
            ApplyPortraitOverrides();
            if (AssetDatabase.LoadAssetAtPath<ShadowDatabase>("Assets/Resources/ShadowDatabase.asset") != null)
                ShadowRosterArtBatch.CreateRegistry();
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
                Sprite sprite = PortraitFor(shadow.shadowId);
                if (sprite != null) shadow.silhouetteSprite = sprite;
                var restored = PortraitFor(shadow.shadowId + "_restored");
                if (restored != null && shadow.restoredForm != null && shadow.restoredForm.enabled)
                    shadow.restoredForm.silhouetteSprite = restored;
                EvolutionArtCatalog.ApplyTo(shadow);
                Sprite[] fieldFrames = FieldFramesFor(shadow.shadowId);
                if (fieldFrames != null) shadow.fieldFrames = fieldFrames;
                else if (shadow.fieldFrames != null && shadow.fieldFrames.Any(frame =>
                             frame == null || frame.rect.width != 24 || frame.rect.height != 32))
                    shadow.fieldFrames = Array.Empty<Sprite>();
                EditorUtility.SetDirty(shadow);
            }
        }

        public static Sprite PortraitFor(string id) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitFolder}/{id}.png") ?? ShadowPortraitAtlasImporter.Portrait(id);

        public static bool HasDedicatedPortrait(string id) => PortraitFor(id) != null;

        public static Sprite[] FieldFramesFor(string id)
        {
            string firstPath = $"{FieldFolder}/{id}_01.png";
            string secondPath = $"{FieldFolder}/{id}_02.png";
            if (!IsGameplayReadableFieldFrame(firstPath, out _) ||
                !IsGameplayReadableFieldFrame(secondPath, out _)) return null;
            var first = AssetDatabase.LoadAssetAtPath<Sprite>(firstPath);
            var second = AssetDatabase.LoadAssetAtPath<Sprite>(secondPath);
            return first != null && second != null ? new[] { first, second } : null;
        }

        /// <summary>
        /// 24x32 필드에서 실루엣이 깨져 보이는 원인인 반투명 가장자리,
        /// 떨어진 단독 픽셀, 과도한 색·면적을 차단한다.
        /// 통과하지 못하면 전투 초상 대신 자동 생성된 단순한 2프레임을 사용한다.
        /// </summary>
        public static bool IsGameplayReadableFieldFrame(string path, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { reason = "파일 누락"; return false; }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false) ||
                    texture.width != 24 || texture.height != 32)
                { reason = "24x32 규격 불일치"; return false; }
                Color32[] pixels = texture.GetPixels32();
                var occupied = new HashSet<int>();
                var colors = new HashSet<uint>();
                bool softEdge = false;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 color = pixels[i];
                    if (color.a == 0) continue;
                    if (color.a != 255) softEdge = true;
                    if (color.a < 32) continue;
                    occupied.Add(i);
                    colors.Add((uint)(color.r << 24 | color.g << 16 | color.b << 8 | color.a));
                }
                if (softEdge) { reason = "반투명 가장자리"; return false; }
                if (occupied.Count < 48 || occupied.Count > 240)
                { reason = $"실루엣 면적 {occupied.Count}px"; return false; }
                if (colors.Count > 16) { reason = $"팔레트 {colors.Count}색"; return false; }

                int total = occupied.Count, largest = 0, components = 0;
                var queue = new Queue<int>();
                while (occupied.Count > 0)
                {
                    int start = occupied.First(); occupied.Remove(start); queue.Enqueue(start);
                    int size = 0; components++;
                    while (queue.Count > 0)
                    {
                        int index = queue.Dequeue(); size++;
                        int x = index % 24, y = index / 24;
                        TryVisit(index - 1, x > 0); TryVisit(index + 1, x < 23);
                        TryVisit(index - 24, y > 0); TryVisit(index + 24, y < 31);
                    }
                    largest = Mathf.Max(largest, size);
                }
                if (components > 3 || largest < Mathf.CeilToInt(total * .96f))
                { reason = $"분리 픽셀 덩어리 {components}개"; return false; }
                return true;

                void TryVisit(int index, bool valid)
                {
                    if (valid && occupied.Remove(index)) queue.Enqueue(index);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        public static bool HasDedicatedFieldArt(ShadowData shadow)
        {
            if (shadow == null) return false;
            Sprite[] frames = FieldFramesFor(shadow.shadowId);
            return frames != null && shadow.fieldFrames != null && frames.All(frame => shadow.fieldFrames.Contains(frame));
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
                bool formPortrait = false;
                foreach (string suffix in new[] { "_restored", "_salvation", "_grudge" })
                    if (id.EndsWith(suffix, StringComparison.Ordinal) &&
                        knownIds.Contains(id.Substring(0, id.Length - suffix.Length))) formPortrait = true;
                if (!knownIds.Contains(id) && !formPortrait) invalid.Add("알 수 없는 그림자 초상 파일명: " + path.Replace('\\', '/'));
            }
            foreach (string path in Directory.GetFiles(FieldFolder, "*.png"))
            {
                string stem = Path.GetFileNameWithoutExtension(path);
                if (stem.Length < 4) continue;
                string id = stem.Substring(0, stem.Length - 3);
                if (!knownIds.Contains(id)) invalid.Add("알 수 없는 그림자 필드 파일명: " + path.Replace('\\', '/'));
            }
            int portraits = ids.Count(HasDedicatedPortrait);
            int actors = database != null ? database.shadows.Count(HasDedicatedFieldArt) : 0;
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
            missing.AddRange(ids.Where(id => !HasDedicatedPortrait(id))
                .Take(12).Select(id => "Portraits/" + id + ".png"));
            if (database != null) missing.AddRange(database.shadows.Where(x => !HasDedicatedFieldArt(x))
                .Take(12).Select(x => "Field binding/" + x.shadowId));
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
                if (folder == PortraitFolder || folder == "Assets/Resources/Art/Evolution/Portraits")
                {
                    var mobile = importer.GetPlatformTextureSettings("Android");
                    mobile.name = "Android"; mobile.overridden = true; mobile.maxTextureSize = 1024;
                    mobile.format = TextureImporterFormat.ETC2_RGBA8; mobile.compressionQuality = 75;
                    importer.SetPlatformTextureSettings(mobile);
                }
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
