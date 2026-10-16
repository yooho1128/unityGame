#if UNITY_EDITOR
using System;
using System.IO;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    public static class ReleaseVisualAssetGenerator
    {
        public static void Generate()
        {
            const string folder = "Assets/Resources/Art/FX";
            Directory.CreateDirectory(folder);
            foreach (BattleFxGlyph glyph in Enum.GetValues(typeof(BattleFxGlyph)))
            {
                string path = $"{folder}/{glyph}.png";
                if (!File.Exists(path))
                {
                    var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                    texture.SetPixels32(BattleFxSpriteLibrary.Pixels(glyph)); texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                }
                ImportSprite(path, true);
            }
            const string backgrounds = "Assets/Resources/Art/BattleBackgrounds";
            if (Directory.Exists(backgrounds)) foreach (string path in Directory.GetFiles(backgrounds, "*.png")) ImportSprite(path, false);
        }

        private static void ImportSprite(string path, bool glyph)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = glyph ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = glyph ? 64 : 2048;
            importer.filterMode = glyph ? FilterMode.Bilinear : FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
            if (!glyph)
            {
                var mobile = importer.GetPlatformTextureSettings("Android");
                mobile.name = "Android"; mobile.overridden = true; mobile.maxTextureSize = 2048;
                mobile.format = TextureImporterFormat.ETC2_RGB4; mobile.compressionQuality = 75;
                importer.SetPlatformTextureSettings(mobile); importer.SaveAndReimport();
            }
        }
    }
}
#endif
