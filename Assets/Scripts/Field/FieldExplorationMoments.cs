using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowTheater.Field
{
    /// <summary>한 지역을 충분히 직접 걸어 본 순간을 비전투 추억과 동행 유대로 기록한다.</summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class FieldExplorationMoments : MonoBehaviour
    {
        [SerializeField, Min(4)] private int uniqueCellsRequired = 12;
        [SerializeField, Min(1)] private int bondExperience = 2;
        private readonly HashSet<Vector2Int> _visited = new HashSet<Vector2Int>();
        private PlayerController _player;
        private RegionData _region;
        private bool _claimed;

        private IEnumerator Start()
        {
            _player = GetComponent<PlayerController>();
            while (SaveManager.Current == null) yield return null;
            _region = RegionRepository.GetByScene(SceneManager.GetActiveScene().name);
            if (_region == null || string.IsNullOrEmpty(_region.regionId)) yield break;
            _claimed = SaveManager.HasFlag(Flag);
            _visited.Add(_player.Cell);
            _player.OnStepFinished += Remember;
        }

        private void OnDestroy()
        {
            if (_player != null) _player.OnStepFinished -= Remember;
        }

        private void Remember(Vector2Int cell)
        {
            if (_claimed || !_visited.Add(cell) || _visited.Count < uniqueCellsRequired) return;
            _claimed = true;
            SaveManager.SetFlag(Flag);
            ShadowInstance companion = CompanionBondService.CurrentCompanion();
            if (companion != null)
                CompanionBondService.AddExperience(companion.instanceId, bondExperience, out _, out _);
            SaveManager.Instance?.TrySave(false);
            string place = L10n.Text(_region.displayName);
            SaveFeedbackController.Instance?.ShowMessage(
                $"{place}의 풍경을 마음에 새겼습니다 · 동행 유대 +{bondExperience}");
        }

        private string Flag => "scenic_memory_" + _region.regionId;
    }
}
