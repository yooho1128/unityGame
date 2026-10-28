using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>NPC-only art and stable identities. Atlas cells are top-left row-major.</summary>
    public static class NpcIdentityCatalog
    {
        [Serializable] public class Entry
        {
            public string id, name, role, region, appearance;
            public int sheet, cell;
            public int cropTop, cropHeight;
        }
        [Serializable] private class Catalog { public int columns = 4, rows = 4; public List<Entry> entries; }
        private static Catalog catalog;
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static IReadOnlyList<Entry> All { get { Load(); return catalog.entries; } }
        private static void Load()
        {
            if (catalog != null) return;
            var asset = Resources.Load<TextAsset>("Data/NpcIdentityCatalog");
            if (asset == null) throw new InvalidOperationException("Missing NPC identity catalog");
            catalog = JsonUtility.FromJson<Catalog>(asset.text);
        }
        public static Entry Find(string id)
        {
            Load();
            return catalog.entries.Find(e => e.id == id);
        }
        public static Sprite Portrait(string id)
        {
            if (sprites.TryGetValue(id, out var sprite)) return sprite;
            var entry = Find(id);
            if (entry == null) return null;
            var texture = Resources.Load<Texture2D>($"Art/Npcs/npc_atlas_{entry.sheet:00}");
            if (texture == null) return null;
            float width = texture.width / (float)catalog.columns, height = texture.height / (float)catalog.rows;
            float cropHeight = entry.cropHeight > 0 ? entry.cropHeight : height;
            var rect = new Rect(entry.cell % catalog.columns * width,
                texture.height - entry.cell / catalog.columns * height - entry.cropTop - cropHeight, width, cropHeight);
            sprite = Sprite.Create(texture, rect, new Vector2(.5f, 0f), cropHeight, 0, SpriteMeshType.FullRect);
            sprite.name = entry.id;
            sprites[id] = sprite;
            return sprite;
        }
    }
}
