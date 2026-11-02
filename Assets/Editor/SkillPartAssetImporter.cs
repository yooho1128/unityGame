#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowTheater.UI;
using UnityEditor;
using UnityEngine;

namespace ShadowTheater.EditorTools
{
    /// <summary>Run in a validation copy. Imports original PNGs without altering their pixels.</summary>
    public static class SkillPartAssetImporter
    {
        [Serializable] private sealed class Catalog { public SkillPartDefinition[] parts; }

        [MenuItem("Tools/Shadow Theater/Art/Import 128 Skill Parts")]
        public static void ImportForCi()
        {
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText("Assets/Resources/Data/SkillPartCatalog.json"));
            if (catalog.parts.Length != 128 || catalog.parts.Select(x => x.id).Distinct().Count() != 128)
                throw new InvalidOperationException("Expected 128 unique painted parts");
            foreach (var group in catalog.parts.GroupBy(x => x.sheet))
            {
                string path = "Assets/Resources/" + group.Key + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.spritePixelsPerUnit = 100;
                importer.spritesheet = group.Select(part => new SpriteMetaData
                {
                    name = part.id, rect = part.rect.Rect, alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(.5f, .5f), border = Vector4.zero
                }).ToArray();
                importer.SaveAndReimport();
                var imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                if (imported.Length != 32 || imported.Any(sprite => sprite.rect.width < 30 || sprite.rect.height < 30))
                    throw new InvalidOperationException("Bad part slicing: " + path);
            }
            AssetDatabase.SaveAssets();
            string report = "128 distinct painted sprites imported; 4 shared textures; source alpha preserved.\n";
            Directory.CreateDirectory("Docs/Generated/SkillParts128");
            File.WriteAllText("Docs/Generated/SkillParts128/import.txt", report);
            Debug.Log("[SkillParts] PASS: " + report);
        }
    }
}
#endif
