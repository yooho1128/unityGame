using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.UI
{
    public readonly struct SkillArtSet
    {
        public readonly string charge, travel, impact, debris, crest;
        public bool IsComplete => !string.IsNullOrEmpty(charge) && !string.IsNullOrEmpty(travel) &&
            !string.IsNullOrEmpty(impact) && !string.IsNullOrEmpty(debris) && !string.IsNullOrEmpty(crest);
        public SkillArtSet(string charge, string travel, string impact, string debris, string crest)
        { this.charge = charge; this.travel = travel; this.impact = impact; this.debris = debris; this.crest = crest; }
    }

    [Serializable]
    public sealed class SkillPartBounds
    {
        public int x, y, width, height;
        public Rect Rect => new Rect(x, y, width, height);
    }

    [Serializable]
    public sealed class SkillPartDefinition
    {
        public string id, sheet, category, description, sha256;
        public int column, row, visiblePixels;
        public SkillPartBounds rect;
    }

    /// <summary>128 painted sub-sprites from four shared textures. Missing art keeps the geometric fallback usable.</summary>
    public static class SkillPartLibrary
    {
        [Serializable] private sealed class Catalog { public SkillPartDefinition[] parts; }
        private static Dictionary<string, Sprite> sprites;
        private static SkillPartDefinition[] definitions;
        private static readonly HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
        public static IReadOnlyList<SkillPartDefinition> Definitions { get { Preload(); return definitions; } }
        public static IReadOnlyCollection<string> UsedIds => used;
        public static int SpriteCount { get { Preload(); return sprites.Count; } }

        public static void Preload()
        {
            if (sprites != null) return;
            sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            var json = Resources.Load<TextAsset>("Data/SkillPartCatalog");
            definitions = json != null ? JsonUtility.FromJson<Catalog>(json.text).parts : Array.Empty<SkillPartDefinition>();
            var sheets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var part in definitions) sheets.Add(part.sheet);
            foreach (string sheet in sheets)
                foreach (Sprite sprite in Resources.LoadAll<Sprite>(sheet)) sprites.Add(sprite.name, sprite);
        }

        public static Sprite Get(string id)
        {
            Preload();
            return id != null && sprites.TryGetValue(id, out Sprite sprite) ? sprite : null;
        }

        public static void RecordDraw(string id) { if (id != null) used.Add(id); }
        public static void ResetUsage() => used.Clear();
    }
}
