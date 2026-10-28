using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowTheater.Data
{
    /// <summary>Stage art is resolved by stable species ID and saved stage, even for previously generated scenes.</summary>
    public static class EvolutionArtCatalog
    {
        [Serializable] public class Entry
        {
            public string shadowId, stage, formName, portrait;
            public string[] fieldFrames;
        }
        [Serializable] private class Catalog { public Entry[] entries; }
        private static Dictionary<string, Entry> entries;
        private static readonly Dictionary<string, Sprite> portraits = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite[]> frames = new Dictionary<string, Sprite[]>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        { entries = null; portraits.Clear(); frames.Clear(); }

        private static void Load()
        {
            if (entries != null) return;
            entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>("Data/EvolutionArtCatalog");
            if (asset == null) return;
            var catalog = JsonUtility.FromJson<Catalog>(asset.text);
            foreach (var entry in catalog?.entries ?? Array.Empty<Entry>())
                entries.Add(entry.shadowId + "/" + entry.stage, entry);
        }
        public static Entry[] All
        {
            get { Load(); var result = new Entry[entries.Count]; entries.Values.CopyTo(result, 0); return result; }
        }
        public static string Stage(ShadowInstance instance)
        {
            if (instance?.ActiveForm == null || !instance.ActiveForm.enabled) return null;
            if (instance.memoryStage == MemoryStage.Restored) return "restored";
            if (instance.memoryStage == MemoryStage.TrueName)
                return instance.awakeningPath == AwakeningPath.Grudge ? "grudge" : "salvation";
            return null;
        }
        public static Entry Find(string id, string stage)
        {
            Load();
            return stage != null && entries.TryGetValue(id + "/" + stage, out var entry) ? entry : null;
        }
        public static Sprite Portrait(string id, string stage)
        {
            var entry = Find(id, stage);
            if (entry == null) return null;
            string key = id + "/" + stage;
            if (!portraits.TryGetValue(key, out var sprite))
            {
                sprite = Resources.Load<Sprite>(entry.portrait);
                if (sprite != null) portraits[key] = sprite;
            }
            return sprite;
        }
        public static Sprite[] FieldFrames(string id, string stage)
        {
            var entry = Find(id, stage);
            if (entry == null || entry.fieldFrames == null || entry.fieldFrames.Length != 2) return null;
            string key = id + "/" + stage;
            if (frames.TryGetValue(key, out var result)) return result;
            result = new[] { Resources.Load<Sprite>(entry.fieldFrames[0]), Resources.Load<Sprite>(entry.fieldFrames[1]) };
            if (result[0] == null || result[1] == null) return null;
            frames[key] = result;
            return result;
        }
        // Editor generators may bind persistent imported sprites into regenerated ScriptableObjects.
        // Runtime instance lookups above stay lazy, so existing databases need no bulk preload.
        public static void ApplyTo(ShadowData shadow)
        {
            if (shadow == null) return;
            Bind(shadow.shadowId, "restored", shadow.restoredForm);
            Bind(shadow.shadowId, "salvation", shadow.salvationForm);
            Bind(shadow.shadowId, "grudge", shadow.grudgeForm);
        }
        private static void Bind(string id, string stage, MemoryFormData form)
        {
            if (form == null || !form.enabled) return;
            var portrait = Portrait(id, stage); var field = FieldFrames(id, stage);
            if (portrait != null) form.silhouetteSprite = portrait;
            if (field != null) form.fieldFrames = field;
        }
    }
}
