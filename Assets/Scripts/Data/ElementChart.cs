using System;
using UnityEngine;

namespace ShadowTheater.Data
{
    /// <summary>Game, AI and codex share ElementCatalog. Existing enum values are never reassigned.</summary>
    public static class ElementChart
    {
        public const float Strong = 1.5f, Weak = .75f, Neutral = 1f;
        [Serializable] public class Rule
        {
            public string id, name, en, color;
            public int value;
            public string[] strong;
        }
        [Serializable] private class Catalog { public Rule[] elements; }
        private static Rule[] _rules;
        public static Rule[] Rules
        {
            get
            {
                if (_rules == null)
                {
                    var asset = Resources.Load<TextAsset>("Data/ElementCatalog");
                    if (asset == null) throw new InvalidOperationException("Missing Data/ElementCatalog");
                    _rules = JsonUtility.FromJson<Catalog>(asset.text).elements;
                }
                return _rules;
            }
        }
        public static Rule For(ShadowElement element) => Array.Find(Rules, r => r.value == (int)element);
        public static string Name(ShadowElement element) => For(element)?.name ?? element.ToString();
        public static Color ColorFor(ShadowElement element) => ColorUtility.TryParseHtmlString(For(element)?.color, out var c) ? c : Color.white;
        public static float GetMultiplier(ShadowElement attack, ShadowElement defend)
        {
            if (attack == defend) return Neutral;
            // Strong is checked first: Light/Dark and Psychic/None are intentionally mutual.
            if (Array.IndexOf(For(attack)?.strong ?? Array.Empty<string>(), defend.ToString()) >= 0) return Strong;
            if (Array.IndexOf(For(defend)?.strong ?? Array.Empty<string>(), attack.ToString()) >= 0) return Weak;
            return Neutral;
        }
    }
}
