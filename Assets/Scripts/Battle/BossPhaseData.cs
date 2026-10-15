using System;
using System.Collections.Generic;
using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.Battle
{
    [Serializable]
    public class BossPhaseCatalogData
    {
        public List<BossPhaseDefinition> phases = new List<BossPhaseDefinition>();
    }

    [Serializable]
    public class BossPhaseDefinition
    {
        public string phaseId;
        public string bossShadowId;
        [Range(.1f, .9f)] public float hpThreshold = .5f;
        public string message;
        [Range(0, 5)] public int enemyFpGain = 2;
        [Range(0, 5)] public int playerFpDrain;
        [Range(0f, .5f)] public float healRatio;
        public bool cleanseBoss;
        public string playerStatus;
        [Range(1, 5)] public int statusDuration = 2;

        public bool TryGetPlayerStatus(out StatusEffectType status) =>
            Enum.TryParse(playerStatus, true, out status) && status != StatusEffectType.None;
    }

    public static class BossPhaseRepository
    {
        private const string ResourcePath = "Data/BossPhaseCatalog";
        private static Dictionary<string, BossPhaseDefinition> _byShadowId;

        public static BossPhaseDefinition Get(string shadowId)
        {
            EnsureLoaded();
            return shadowId != null && _byShadowId.TryGetValue(shadowId, out BossPhaseDefinition phase)
                ? phase : null;
        }

        public static void Reload() => _byShadowId = null;

        private static void EnsureLoaded()
        {
            if (_byShadowId != null) return;
            _byShadowId = new Dictionary<string, BossPhaseDefinition>(StringComparer.Ordinal);
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) return;
            BossPhaseCatalogData catalog = JsonUtility.FromJson<BossPhaseCatalogData>(asset.text);
            foreach (BossPhaseDefinition phase in catalog?.phases ?? new List<BossPhaseDefinition>())
                if (phase != null && !string.IsNullOrWhiteSpace(phase.bossShadowId) &&
                    !_byShadowId.ContainsKey(phase.bossShadowId))
                    _byShadowId.Add(phase.bossShadowId, phase);
        }
    }
}
