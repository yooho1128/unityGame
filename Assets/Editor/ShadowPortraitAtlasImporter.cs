#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShadowTheater.EditorTools
{
    /// <summary>종별 ID를 스프라이트 시트의 독립 영역과 연결한다. 파일 수와 종 수를 구분한다.</summary>
    public static class ShadowPortraitAtlasImporter
    {
        public const string ManifestPath = "Assets/Art/Final/PortraitAtlases/RosterManifest.json";
        private static Dictionary<string, Sprite> _portraits;
        [Serializable] public class Manifest
        { public List<Sheet> sheets = new List<Sheet>(); public string[] reserveIds = Array.Empty<string>(); }
        [Serializable] public class Sheet
        { public string path; public int columns, rows; public string[] ids; }

        public static Manifest ReadManifest() => File.Exists(ManifestPath)
            ? JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)) : new Manifest();

        public static void Import(List<string> errors, bool requireAll = true)
        {
            _portraits = null;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sheet in ReadManifest().sheets)
            {
                if (!File.Exists(sheet.path)) { if (requireAll) errors.Add("그림자 시트 누락: " + sheet.path); continue; }
                if (sheet.columns < 1 || sheet.rows < 1 || sheet.ids == null || sheet.ids.Length > sheet.columns * sheet.rows)
                { errors.Add("그림자 시트 배치 오류: " + sheet.path); continue; }
                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(File.ReadAllBytes(sheet.path));
                var slices = new SpriteMetaData[sheet.ids.Length];
                for (int i = 0; i < sheet.ids.Length; i++)
                {
                    string id = sheet.ids[i];
                    if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) errors.Add("그림자 시트 ID 중복/누락: " + id);
                    int col = i % sheet.columns, row = sheet.rows - 1 - i / sheet.columns;
                    int x0 = col * source.width / sheet.columns, x1 = (col + 1) * source.width / sheet.columns;
                    int y0 = row * source.height / sheet.rows, y1 = (row + 1) * source.height / sheet.rows;
                    int opaque = 0, transparent = 0;
                    for (int y = y0; y < y1; y += 2)
                    for (int x = x0; x < x1; x += 2)
                    {
                        float alpha = source.GetPixel(x, y).a;
                        if (alpha > .5f) opaque++;
                        if (alpha < .05f) transparent++;
                    }
                    if (x1 - x0 < 192 || y1 - y0 < 192 || opaque < 512 || transparent < 64)
                        errors.Add("그림자 시트 빈 칸/해상도/알파 오류: " + id);
                    slices[i] = new SpriteMetaData { name = id, rect = new Rect(x0, y0, x1 - x0, y1 - y0),
                        alignment = (int)SpriteAlignment.BottomCenter, pivot = new Vector2(.5f, 0) };
                }
                Object.DestroyImmediate(source);
                AssetDatabase.ImportAsset(sheet.path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(sheet.path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritesheet = slices;
                importer.spritePixelsPerUnit = 256;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point; importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
                var android = importer.GetPlatformTextureSettings("Android");
                android.name = "Android"; android.overridden = true; android.maxTextureSize = 2048;
                android.format = TextureImporterFormat.ETC2_RGBA8; android.compressionQuality = 85;
                importer.SetPlatformTextureSettings(android); importer.SaveAndReimport();
            }
        }

        public static void ImportAvailableForCi()
        {
            var errors = new List<string>(); Import(errors, false);
            if (errors.Count > 0) throw new UnityEditor.Build.BuildFailedException(string.Join("\n", errors));
            Debug.Log("[ShadowAtlas] PASS: available transparent species sheets");
        }
        public static Sprite Portrait(string id)
        {
            if (_portraits == null)
            {
                _portraits = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (var sheet in ReadManifest().sheets)
                    foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(sheet.path).OfType<Sprite>())
                        _portraits[sprite.name] = sprite;
            }
            return id != null && _portraits.TryGetValue(id, out var value) ? value : null;
        }
    }
}
#endif
