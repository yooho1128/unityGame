using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>걸음 기반 랜덤 조우. 현재 지역의 대표/확장 로스터 외 그림자는 선택하지 않는다.</summary>
    public class RandomEncounterController : MonoBehaviour
    {
        [SerializeField] private string regionId;
        [SerializeField, Min(1)] private int safeStepsMin = 6;
        [SerializeField, Min(1)] private int safeStepsMax = 10;
        [SerializeField, Range(0f, 1f)] private float chancePerStep = .12f;
        [SerializeField, Min(1)] private int pitySteps = 18;

        private readonly List<EncounterCandidate> _candidates = new List<EncounterCandidate>();
        private PlayerController _player;
        private int _safeSteps;
        private int _eligibleSteps;
        private bool _battlePending;
        private string _companionInstanceId;

        private IEnumerator Start()
        {
            yield return null;
            RegionData region = RegionRepository.Get(regionId);
            if (region == null || !region.AllowsRandomEncounters) { enabled = false; yield break; }
            BuildCandidates(region);
            if (_candidates.Count == 0)
            {
                Debug.LogWarning($"[RandomEncounter] {regionId} 지역 조우 후보가 없습니다.");
                enabled = false;
                yield break;
            }
            _player = PlayerController.Instance;
            if (_player == null) { enabled = false; yield break; }
            _player.OnStepFinished += OnStepFinished;
            GameFlowController.Instance.OnBattleFlowFinished += OnBattleFinished;
            _companionInstanceId = SaveManager.Current?.companionInstanceId;
            SaveManager.PartyChanged += OnPartyChanged;
            ResetCounter();
        }

        private void OnDestroy()
        {
            if (_player != null) _player.OnStepFinished -= OnStepFinished;
            if (GameFlowController.Instance != null)
                GameFlowController.Instance.OnBattleFlowFinished -= OnBattleFinished;
            SaveManager.PartyChanged -= OnPartyChanged;
        }

        private void OnStepFinished(Vector2Int _)
        {
            if (_battlePending || GameFlowController.Instance == null ||
                !GameFlowController.Instance.CanStartEncounter) return;
            if (_safeSteps-- > 0) return;
            _eligibleSteps++;
            CompanionPassiveSnapshot passive = CompanionBondService.CurrentPassive();
            float chance = chancePerStep * passive.encounterMultiplier * CompanionBondService.EncounterChanceMultiplier(
                CompanionBondService.CurrentCompanionShadowId());
            FieldWorldSnapshot world = FieldWorldState.Current(RegionRepository.Get(regionId));
            chance *= FieldWorldState.EncounterRateMultiplier(world);
            if (_eligibleSteps < pitySteps && UnityEngine.Random.value > chance) return;

            RegionData region = RegionRepository.Get(regionId);
            EncounterCandidate candidate = ChooseCandidate(region);
            if (candidate == null) return;
            int minLevel = Mathf.Max(1, candidate.minLevel);
            int maxLevel = Mathf.Max(minLevel, candidate.maxLevel);
            int level = UnityEngine.Random.Range(minLevel, maxLevel + 1);
            level = IntroEncounterPolicy.ClampLevel(region, level, SaveManager.Current?.party);
            _battlePending = true;
            StartCoroutine(BeginEncounter(candidate, level, region, world));
        }

        private IEnumerator BeginEncounter(EncounterCandidate candidate, int level, RegionData region,
            FieldWorldSnapshot world)
        {
            if (candidate.shadow.growthTier == GrowthTier.Rare)
            {
                SaveFeedbackController.Instance?.ShowMessage(FieldWorldState.RareEncounterSignal(world));
                yield return new WaitForSecondsRealtime(.8f);
            }
            if (GameFlowController.Instance == null || !GameFlowController.Instance.CanStartEncounter)
            { _battlePending = false; yield break; }
            GameFlowController.Instance.StartRandomBattle(
                new List<ShadowInstance> { new ShadowInstance(candidate.shadow, level) }, region);
        }

        private void OnBattleFinished(BattleContext context, BattleOutcome outcome)
        {
            if (!_battlePending || context?.mode != BattleMode.Wild) return;
            _battlePending = false;
            ResetCounter();
        }

        private void OnPartyChanged()
        {
            string current = SaveManager.Current?.companionInstanceId;
            if (current == _companionInstanceId) return;
            _companionInstanceId = current;
            ResetCounter();
        }

        private void ResetCounter()
        {
            int bonus = CompanionBondService.CurrentPassive().safeStepBonus +
                        CompanionBondService.EncounterSafeStepBonus(CompanionBondService.CurrentCompanionShadowId());
            _safeSteps = UnityEngine.Random.Range(safeStepsMin, Mathf.Max(safeStepsMin + 1, safeStepsMax + 1)) + bonus;
            _eligibleSteps = 0;
        }

        private void BuildCandidates(RegionData region)
        {
            ShadowDatabase database = ShadowDatabase.Instance;
            if (database == null) return;
            var byId = new Dictionary<string, EncounterCandidate>(StringComparer.Ordinal);
            TextAsset manifestAsset = Resources.Load<TextAsset>("Data/RegionalRosterManifest");
            if (manifestAsset != null)
            {
                RegionalRosterManifest manifest = JsonUtility.FromJson<RegionalRosterManifest>(manifestAsset.text);
                foreach (RegionalRosterEntry entry in manifest?.entries ?? new List<RegionalRosterEntry>())
                {
                    if (entry.regionId != region.regionId || string.IsNullOrEmpty(entry.shadowId)) continue;
                    ShadowData shadow = database.GetShadow(entry.shadowId);
                    AddCandidate(byId, region, shadow, entry.featured ? 2f : 1f,
                        entry.minLevel, entry.maxLevel);
                }
            }
            string[] featured = region.featuredShadows ?? Array.Empty<string>();
            for (int rank = 0; rank < featured.Length; rank++)
            {
                string name = featured[rank];
                ShadowData shadow = database.shadows.FirstOrDefault(x => x != null && x.displayName == name);
                AddCandidate(byId, region, shadow, RegionalEncounterPolicy.FeaturedWeight(rank),
                    region.recommendedLevelMin, region.recommendedLevelMax);
            }
            _candidates.AddRange(byId.Values.OrderBy(x => x.shadow.shadowId));
        }

        private static void AddCandidate(Dictionary<string, EncounterCandidate> byId, RegionData region,
            ShadowData shadow, float baseWeight, int minLevel, int maxLevel)
        {
            if (!RegionalEncounterPolicy.IsEligible(region, shadow)) return;
            float weight = baseWeight * RegionalEncounterPolicy.RarityMultiplier(shadow.growthTier);
            if (weight <= 0f) return;
            if (byId.TryGetValue(shadow.shadowId, out EncounterCandidate existing))
            {
                existing.weight = Mathf.Max(existing.weight, weight);
                existing.minLevel = Mathf.Min(existing.minLevel, minLevel);
                existing.maxLevel = Mathf.Max(existing.maxLevel, maxLevel);
                return;
            }
            byId.Add(shadow.shadowId, new EncounterCandidate
            {
                shadow = shadow,
                weight = weight,
                minLevel = minLevel,
                maxLevel = maxLevel
            });
        }

        private EncounterCandidate ChooseCandidate(RegionData region)
        {
            FieldWorldSnapshot world = FieldWorldState.Current(region);
            float total = 0f;
            foreach (EncounterCandidate candidate in _candidates)
                total += EffectiveWeight(candidate, world, region);
            if (total <= 0f) return null;
            float roll = UnityEngine.Random.value * total;
            foreach (EncounterCandidate candidate in _candidates)
            {
                float weight = EffectiveWeight(candidate, world, region);
                if (weight <= 0f) continue;
                roll -= weight;
                if (roll <= 0f) return candidate;
            }
            return _candidates.LastOrDefault(x => EffectiveWeight(x, world, region) > 0f);
        }

        private static float EffectiveWeight(EncounterCandidate candidate, FieldWorldSnapshot world, RegionData region)
        {
            if (!IntroEncounterPolicy.IsAvailable(region, candidate.shadow, SaveManager.Current?.party)) return 0f;
            return Mathf.Max(0f, candidate.weight) *
                   FieldWorldState.EncounterWeightMultiplier(candidate.shadow, world);
        }

        [Serializable] private class RegionalRosterManifest { public List<RegionalRosterEntry> entries; }
        [Serializable] private class RegionalRosterEntry
        {
            public string shadowId;
            public string regionId;
            public int minLevel, maxLevel;
            public bool featured;
        }
        private class EncounterCandidate
        {
            public ShadowData shadow;
            public int minLevel, maxLevel;
            public float weight;
        }
    }
}
