#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowTheater.Field;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace ShadowTheater.EditorTools
{
    public static class AssetCompletionBatch
    {
        private const string ArtRoot = "Assets/Resources/Art/Environment/";
        private const string AudioRoot = "Assets/Resources/Audio/Feedback/";

        public static void PrepareAndBuildForCi()
        {
            ImportAtlas("ReleaseTerrain", 3, 2, new[] { "Grass", "Path", "Water", "Stone", "Wood", "Brick" });
            ImportAtlas("ReleaseProps", 2, 2, new[] { "Bush", "Cliff", "Flowers", "Tree" });
            GenerateFeedbackAudio();
            MobileBuildConfigurator.Apply();
            GameRegressionValidator.ValidateForCi();
            MobilePlayabilityChecks.CheckAndCaptureForCi();
            ReleaseReadinessBatch.BuildAndroidQaForCi();
        }

        private static void ImportAtlas(string name, int columns, int rows, string[] names)
        {
            string path = ArtRoot + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            int width = texture.width / columns, height = texture.height / rows;
            bool valid = width == height && texture.width % columns == 0 && texture.height % rows == 0;
            Object.DestroyImmediate(texture);
            if (!valid)
                throw new InvalidDataException("Atlas cell geometry: " + path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = names.Select((item, i) => new SpriteMetaData
            {
                name = item, rect = new Rect(i % columns * width, (rows - 1 - i / columns) * height, width, height),
                alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * .5f
            }).ToArray();
            importer.spritePixelsPerUnit = width;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = name == "ReleaseProps";
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android"; android.overridden = true; android.maxTextureSize = 2048;
            android.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        private static void GenerateFeedbackAudio()
        {
            Directory.CreateDirectory(AudioRoot);
            foreach (FeedbackCue cue in Enum.GetValues(typeof(FeedbackCue)))
            {
                int[] notes;
                float spacing, tail;
                switch (cue)
                {
                    case FeedbackCue.Confirm: notes = new[] { 79, 84 }; spacing = .035f; tail = .07f; break;
                    case FeedbackCue.Record: notes = new[] { 72, 76, 79, 84 }; spacing = .12f; tail = .35f; break;
                    case FeedbackCue.Heal: notes = new[] { 60, 64, 67, 72, 76 }; spacing = .10f; tail = .36f; break;
                    case FeedbackCue.Escape: notes = new[] { 79, 84, 88 }; spacing = .045f; tail = .10f; break;
                    case FeedbackCue.Faint: notes = new[] { 55, 50, 43 }; spacing = .09f; tail = .22f; break;
                    default: notes = new[] { 62, 57 }; spacing = .08f; tail = .13f; break;
                }
                const int rate = 44100;
                float length = (notes.Length - 1) * spacing + tail;
                var samples = new float[Mathf.CeilToInt(length * rate)];
                for (int note = 0; note < notes.Length; note++)
                {
                    float frequency = 440 * Mathf.Pow(2, (notes[note] - 69) / 12f);
                    for (int i = Mathf.RoundToInt(note * spacing * rate); i < samples.Length; i++)
                    {
                        float t = i / (float)rate - note * spacing;
                        float phase = 2 * Mathf.PI * frequency * t;
                        float envelope = Mathf.Clamp01(t / .007f) * Mathf.Exp(-t * 12);
                        float fade = Mathf.Clamp01((length - i / (float)rate) / .025f);
                        samples[i] += (Mathf.Sin(phase) + .20f * Mathf.Sin(phase * 2) + .08f * Mathf.Sin(phase * 3)) * envelope * fade;
                    }
                }
                float peak = samples.Max(x => Mathf.Abs(x));
                float level = cue == FeedbackCue.Confirm ? .4f : .70f;
                string path = AudioRoot + cue + ".wav";
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    int size = samples.Length * 2;
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + size);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                    writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
                    writer.Write((short)2); writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(size);
                    foreach (float sample in samples) writer.Write((short)Mathf.RoundToInt(sample / peak * level * 32767));
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                importer.forceToMono = true;
                importer.defaultSampleSettings = new AudioImporterSampleSettings
                {
                    loadType = AudioClipLoadType.DecompressOnLoad, compressionFormat = AudioCompressionFormat.PCM,
                    sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate
                };
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }

        public static void Validate(List<string> errors)
        {
            var sprites = new Dictionary<string, Sprite>();
            foreach (string name in new[] { "ReleaseTerrain", "ReleaseProps" })
                foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(ArtRoot + name + ".png").OfType<Sprite>())
                    sprites[sprite.name] = sprite;
            foreach (string name in new[] { "Grass", "Path", "Water", "Stone", "Wood", "Brick", "Bush", "Cliff", "Flowers", "Tree" })
                if (!sprites.TryGetValue(name, out var sprite) || Mathf.Abs(sprite.bounds.size.x - 1) > .001f ||
                    Mathf.Abs(sprite.bounds.size.y - 1) > .001f) errors.Add("출시 환경 타일 한 칸 규격 오류: " + name);
            foreach (FeedbackCue cue in Enum.GetValues(typeof(FeedbackCue)))
            {
                string path = AudioRoot + cue + ".wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null || clip.channels != 1 || clip.length < .08f)
                { errors.Add("동작 효과음 누락: " + cue); continue; }
                var samples = new float[clip.samples];
                if (!clip.GetData(samples, 0)) { errors.Add("효과음 PCM 읽기 실패: " + cue); continue; }
                double energy = samples.Sum(x => (double)x * x), mean = samples.Average(x => (double)x);
                if (samples.Max(x => Mathf.Abs(x)) > .90f || Math.Sqrt(energy / samples.Length) < .01 ||
                    Math.Abs(mean) > .02 || Math.Abs(samples[0]) > .01 || Math.Abs(samples[samples.Length - 1]) > .01)
                    errors.Add("효과음 무음/클리핑/DC/경계 오류: " + cue);
            }
            ValidateTileSwap(errors);
        }

        private static void ValidateTileSwap(List<string> errors)
        {
            var host = new GameObject("TerrainTileRegression");
            var tile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                host.AddComponent<Grid>();
                var child = new GameObject("Map", typeof(Tilemap)); child.transform.SetParent(host.transform);
                var map = child.GetComponent<Tilemap>();
                tile.name = "MeadowPixel_Path"; tile.colliderType = Tile.ColliderType.Grid;
                tile.color = new Color(.8f, .9f, 1);
                var position = new Vector3Int(2, -3, 0);
                map.SetTile(position, tile);
                int changed = host.AddComponent<ReleaseTerrainPresentation>().Apply(map);
                var replacement = map.GetTile<Tile>(position);
                if (changed != 1 || replacement == tile || replacement.sprite == null ||
                    replacement.colliderType != tile.colliderType || replacement.color != tile.color ||
                    map.GetUsedTilesCount() != 1 || map.GetTile(Vector3Int.zero) != null)
                    errors.Add("출시 타일 교체가 배치 또는 충돌 정보를 변경함");
                if (host.GetComponent<ReleaseTerrainPresentation>().Apply(map) != 0)
                    errors.Add("출시 타일 중복 교체 발생");
                var portalObject = new GameObject("Portal", typeof(BoxCollider2D), typeof(SpriteRenderer), typeof(MapPortal));
                portalObject.transform.SetParent(host.transform);
                portalObject.transform.localScale = new Vector3(.7f, .9f, 1);
                var collider = portalObject.GetComponent<BoxCollider2D>();
                collider.isTrigger = true; collider.size = new Vector2(1, 2);
                var portal = portalObject.GetComponent<MapPortal>();
                ReleaseTerrainPresentation.StylePortal(portal);
                ReleaseTerrainPresentation.StylePortal(portal);
                var ring = portal.transform.Find("ExitRing");
                if (ring == null || portal.transform.childCount != 1 || collider.size != new Vector2(1, 2) ||
                    !collider.isTrigger || portal.transform.localScale != new Vector3(.7f, .9f, 1) ||
                    Mathf.Abs(ring.GetComponent<SpriteRenderer>().bounds.size.x - .7f) > .001f)
                    errors.Add("출구 아트가 충돌 또는 이동 범위를 변경함");
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(tile); }
            if (errors.Count == 0) Debug.Log("[AssetCompletion] PASS: 10 one-cell sprites, 6 audible bounded SFX, tile geometry/collision preserved");
        }
    }
}
#endif
