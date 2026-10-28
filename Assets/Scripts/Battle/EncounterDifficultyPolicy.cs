using System;
using UnityEngine;

namespace ShadowTheater.Battle
{
    public readonly struct EncounterDifficultyProfile
    {
        public readonly float damage, vulnerability, maxHitRatio;
        public static readonly EncounterDifficultyProfile Neutral = new EncounterDifficultyProfile(1f, 1f, 0f);
        public EncounterDifficultyProfile(float damage, float vulnerability, float maxHitRatio)
        { this.damage = damage; this.vulnerability = vulnerability; this.maxHitRatio = maxHitRatio; }
    }
    /// <summary>Enemy-only encounter tuning. Collection stats and captured shadows remain canonical.</summary>
    public static class EncounterDifficultyPolicy
    {
        [Serializable] private class Chapter
        {
            public int throughAct;
            public float enemyDamage, enemyVulnerability, bossDamage, bossVulnerability, maxHitRatio;
        }
        [Serializable] private class Catalog { public Chapter[] chapters; }
        private static Catalog catalog;
        public static EncounterDifficultyProfile For(int act, BattleMode mode)
        {
            if (act < 1 || act > 8) return EncounterDifficultyProfile.Neutral;
            if (catalog == null)
            {
                var asset = Resources.Load<TextAsset>("Data/BattleDifficultyCatalog");
                if (asset == null) return EncounterDifficultyProfile.Neutral;
                catalog = JsonUtility.FromJson<Catalog>(asset.text);
            }
            foreach (var row in catalog.chapters)
                if (act <= row.throughAct)
                    return new EncounterDifficultyProfile(mode == BattleMode.Boss ? row.bossDamage : row.enemyDamage,
                        mode == BattleMode.Boss ? row.bossVulnerability : row.enemyVulnerability, row.maxHitRatio);
            return EncounterDifficultyProfile.Neutral;
        }
    }
}
