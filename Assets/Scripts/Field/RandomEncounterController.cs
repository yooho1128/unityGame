using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Battle;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.Story;
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

        private readonly List<ShadowData> _candidates = new List<ShadowData>();
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
            float chance = chancePerStep * CompanionBondService.EncounterChanceMultiplier(
                CompanionBondService.CurrentCompanionShadowId());
            if (_eligibleSteps < pitySteps && UnityEngine.Random.value > chance) return;

            RegionData region = RegionRepository.Get(regionId);
            ShadowData shadow = _candidates[UnityEngine.Random.Range(0, _candidates.Count)];
            int level = UnityEngine.Random.Range(region.recommendedLevelMin, region.recommendedLevelMax + 1);
            _battlePending = true;
            GameFlowController.Instance.StartScriptedBattle(BattleMode.Wild,
                new List<ShadowInstance> { new ShadowInstance(shadow, level) }, string.Empty);
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
            int bonus = CompanionBondService.EncounterSafeStepBonus(CompanionBondService.CurrentCompanionShadowId());
            _safeSteps = UnityEngine.Random.Range(safeStepsMin, Mathf.Max(safeStepsMin + 1, safeStepsMax + 1)) + bonus;
            _eligibleSteps = 0;
        }

        private void BuildCandidates(RegionData region)
        {
            ShadowDatabase database = ShadowDatabase.Instance;
            if (database == null) return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            TextAsset manifestAsset = Resources.Load<TextAsset>("Data/RegionalRosterManifest");
            if (manifestAsset != null)
            {
                RegionalRosterManifest manifest = JsonUtility.FromJson<RegionalRosterManifest>(manifestAsset.text);
                foreach (RegionalRosterEntry entry in manifest?.entries ?? new List<RegionalRosterEntry>())
                    if (entry.regionId == region.regionId && !string.IsNullOrEmpty(entry.shadowId)) ids.Add(entry.shadowId);
            }
            foreach (string name in region.featuredShadows ?? Array.Empty<string>())
            {
                ShadowData featured = database.shadows.FirstOrDefault(x => x != null && x.displayName == name);
                if (featured != null) ids.Add(featured.shadowId);
            }
            foreach (string id in ids)
            {
                ShadowData shadow = database.GetShadow(id);
                if (shadow != null && shadow.IsCapturable) _candidates.Add(shadow);
            }
        }

        [Serializable] private class RegionalRosterManifest { public List<RegionalRosterEntry> entries; }
        [Serializable] private class RegionalRosterEntry { public string shadowId; public string regionId; }
    }
}
