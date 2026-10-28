using System;
using UnityEngine;

namespace ShadowTheater.Data
{
    /// <summary>Purchased experience only fills the item's own level band, without banking XP past its cap.</summary>
    public static class TrainingItemPolicy
    {
        [Serializable] public sealed class Rule { public string itemId; public int minimumLevel, maximumLevel; }
        [Serializable] public sealed class Catalog { public Rule[] entries; }
        private static Catalog catalog;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => catalog = null;
        public static Rule Find(string itemId)
        {
            if (catalog == null)
            {
                var asset = Resources.Load<TextAsset>("Data/TrainingItemLimits");
                catalog = asset != null ? JsonUtility.FromJson<Catalog>(asset.text) : new Catalog();
            }
            return Array.Find(catalog?.entries ?? Array.Empty<Rule>(), r => r != null && r.itemId == itemId);
        }
        public static string RangeLabel(Rule rule) =>
            $"Lv.{rule.minimumLevel}~{rule.maximumLevel - 1} 사용 · 최대 Lv.{rule.maximumLevel}";

        public static bool TryGrant(Rule rule, int level, int experience, int requested,
            Func<int, int> experienceToNext, out int granted, out string reason)
        {
            granted = 0;
            if (rule == null || rule.minimumLevel < 1 || rule.maximumLevel <= rule.minimumLevel ||
                rule.maximumLevel > ShadowInstance.MaxLevel || experienceToNext == null || requested <= 0)
            { reason = "이 도구의 성장 구간을 확인할 수 없습니다."; return false; }
            if (level < rule.minimumLevel || level >= rule.maximumLevel)
            { reason = RangeLabel(rule) + " 구간의 그림자에게만 사용할 수 있습니다."; return false; }
            long remaining = -Math.Max(0L, experience);
            for (int current = level; current < rule.maximumLevel; current++) remaining += experienceToNext(current);
            if (remaining <= 0) { reason = "현재 경험치가 이 도구의 성장 상한에 도달했습니다."; return false; }
            granted = (int)Math.Min(remaining, requested);
            reason = RangeLabel(rule);
            return true;
        }
    }
}
