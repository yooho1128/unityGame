#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using ShadowTheater.Data;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    public static class FieldArtAtlasImporter
    {
        public const string Path = "Assets/Resources/Art/Field/ReleaseCast.png";
        private static readonly string[] Names = { "Guide", "Healer", "Merchant", "Scholar", "Messenger", "Guard",
            "Wolf", "Raven", "Marionette", "IceMask", "Wraith", "Knight" };

        public static void Import()
        {
            ImportSheet(Path, 4, 3, Names);
            ImportSheet("Assets/Resources/Art/Field/DirectorWalk.png", 3, 2,
                new[] { "Down0", "Down1", "Up0", "Up1", "Side0", "Side1" });
        }

        private static void ImportSheet(string path, int columns, int rows, string[] names)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Release field cast atlas missing", path);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(path));
            var slices = new SpriteMetaData[names.Length];
            for (int i = 0; i < slices.Length; i++)
            {
                int x0 = i % columns * source.width / columns, x1 = (i % columns + 1) * source.width / columns;
                int y0 = (rows - 1 - i / columns) * source.height / rows, y1 = (rows - i / columns) * source.height / rows;
                int minX = x1, minY = y1, maxX = x0, maxY = y0;
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                    if (source.GetPixel(x, y).a > .1f)
                    { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
                if (maxX <= minX || maxY <= minY) throw new InvalidDataException("Empty field cast cell: " + names[i]);
                slices[i] = new SpriteMetaData { name = names[i], rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                    alignment = (int)SpriteAlignment.BottomCenter, pivot = new Vector2(.5f, 0) };
            }
            UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = slices; importer.spritePixelsPerUnit = 256f;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android"; android.overridden = true; android.maxTextureSize = 2048;
            android.format = TextureImporterFormat.RGBA32; importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        public static Sprite Actor(string name) => AssetDatabase.LoadAllAssetsAtPath(Path).OfType<Sprite>().FirstOrDefault(x => x.name == name);
        public static Sprite PlayerFrame(string name) => AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Art/Field/DirectorWalk.png")
            .OfType<Sprite>().FirstOrDefault(x => x.name == name);
        public static Sprite Monster(ShadowData data)
        {
            string id = data.shadowId.ToLowerInvariant();
            string name = id.Contains("wolf") || id.Contains("beast") || id.Contains("hound") || id.Contains("cat") ? "Wolf"
                : id.Contains("crow") || id.Contains("raven") || id.Contains("bat") ? "Raven"
                : id.Contains("puppet") || id.Contains("thread") || id.Contains("doll") ? "Marionette"
                : id.Contains("mask") || data.element == ShadowElement.Frost ? "IceMask"
                : data.role == ShadowRole.PhysicalDealer || data.role == ShadowRole.Tank ? "Knight" : "Wraith";
            return Actor(name);
        }
    }
}
#endif
