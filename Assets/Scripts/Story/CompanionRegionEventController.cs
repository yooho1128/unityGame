using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>
    /// 현재 동행 그림자와 지역의 조합을 감지한다. 정확한 조건은 UI에 노출하지 않고,
    /// 충분히 탐색하면 확률 판정을 보정하여 비밀 장면을 놓치지 않게 한다.
    /// </summary>
    public class CompanionRegionEventController : MonoBehaviour
    {
        [SerializeField] private string regionId;

        private readonly Dictionary<string, int> _stepsByEvent = new Dictionary<string, int>();
        private CompanionEventCatalogData _catalog;
        private PlayerController _player;
        private string _companionInstanceId;
        private bool _playing;

        private IEnumerator Start()
        {
            _catalog = LoadCatalog();
            while (PlayerController.Instance == null || SaveManager.Current == null || DialogueController.Instance == null)
                yield return null;

            BackfillCompletedRewards();
            _player = PlayerController.Instance;
            if (string.IsNullOrEmpty(regionId))
                regionId = RegionRepository.GetByScene(SaveManager.Current.mapId)?.regionId;
            _companionInstanceId = SaveManager.Current.companionInstanceId;
            _player.OnStepFinished += OnStepFinished;
            SaveManager.PartyChanged += OnPartyChanged;
        }

        private void OnDestroy()
        {
            if (_player != null) _player.OnStepFinished -= OnStepFinished;
            SaveManager.PartyChanged -= OnPartyChanged;
        }

        private static CompanionEventCatalogData LoadCatalog()
        {
            TextAsset json = Resources.Load<TextAsset>("Data/CompanionEventCatalog");
            if (json == null)
            {
                Debug.LogError("[CompanionEvent] CompanionEventCatalog.json을 찾을 수 없습니다.");
                return new CompanionEventCatalogData();
            }
            return JsonUtility.FromJson<CompanionEventCatalogData>(json.text) ?? new CompanionEventCatalogData();
        }

        private void OnPartyChanged()
        {
            string current = SaveManager.Current?.companionInstanceId;
            if (current == _companionInstanceId) return;
            _companionInstanceId = current;
            _stepsByEvent.Clear();
        }

        private void OnStepFinished(Vector2Int _)
        {
            if (_playing || _catalog?.events == null || SaveManager.Current == null) return;
            ShadowInstance companion = CurrentCompanion();
            if (companion == null) return;

            foreach (CompanionEventDefinition definition in _catalog.events)
            {
                if (!CanRun(definition, companion.shadowId)) continue;
                int steps = _stepsByEvent.TryGetValue(definition.eventId, out int count) ? count + 1 : 1;
                _stepsByEvent[definition.eventId] = steps;
                if (steps < Mathf.Max(1, definition.minSteps)) continue;

                bool guaranteed = definition.guaranteedSteps > 0 && steps >= definition.guaranteedSteps;
                if (!guaranteed && Random.value > Mathf.Clamp01(definition.chancePerStep)) continue;
                if (TryPlay(definition)) break;
            }
        }

        private ShadowInstance CurrentCompanion()
        {
            SaveData save = SaveManager.Current;
            if (save?.party == null) return null;
            ShadowInstance companion = save.party.Find(x => x.instanceId == save.companionInstanceId);
            return companion ?? (save.party.Count > 0 ? save.party[0] : null);
        }

        private bool CanRun(CompanionEventDefinition definition, string shadowId)
        {
            if (definition == null || definition.regionId != regionId || definition.requiredShadowId != shadowId)
                return false;
            if (SaveManager.HasFlag(definition.CompletionFlag)) return false;
            if (!string.IsNullOrEmpty(definition.requiredFlag) && !SaveManager.HasFlag(definition.requiredFlag))
                return false;
            return string.IsNullOrEmpty(definition.blockedFlag) || !SaveManager.HasFlag(definition.blockedFlag);
        }

        private bool TryPlay(CompanionEventDefinition definition)
        {
            Color accent = new Color(.75f, .65f, 1f);
            ColorUtility.TryParseHtmlString("#" + definition.accentHex.TrimStart('#'), out accent);
            var dialogue = DialogueController.Instance;
            _playing = dialogue.Play(definition.dialogueId, accent, () => Complete(definition), () => _playing = false)
                && dialogue.IsPlaying;
            return _playing;
        }

        private void Complete(CompanionEventDefinition definition)
        {
            SaveManager.SetFlag(definition.CompletionFlag);
            ApplyPersistentReward(definition);
            if (definition.rewardGold > 0)
                SaveManager.AddGold(definition.rewardGold);
            string feedback = !string.IsNullOrEmpty(definition.rewardMessage)
                ? definition.rewardMessage
                : definition.rewardGold > 0 ? $"동행의 기억을 발견했습니다 · {definition.rewardGold} 금화"
                : "동행의 새로운 기억을 발견했습니다.";
            SaveFeedbackController.Instance?.ShowMessage(feedback);
            SaveManager.Instance?.Save();
            _playing = false;
        }

        private void BackfillCompletedRewards()
        {
            bool changed = false;
            foreach (CompanionEventDefinition definition in _catalog?.events ?? new List<CompanionEventDefinition>())
            {
                if (!SaveManager.HasFlag(definition.CompletionFlag)) continue;
                changed |= ApplyPersistentReward(definition);
            }
            if (changed) SaveManager.Instance?.Save(false);
        }

        private static bool ApplyPersistentReward(CompanionEventDefinition definition)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(definition.rewardFlag))
            {
                changed |= !SaveManager.HasFlag(definition.rewardFlag);
                SaveManager.SetFlag(definition.rewardFlag);
            }
            if (!string.IsNullOrEmpty(definition.rewardItemId) &&
                SaveManager.GetItemCount(definition.rewardItemId) <= 0)
            {
                SaveManager.AddItem(definition.rewardItemId, Mathf.Max(1, definition.rewardItemCount));
                changed = true;
            }
            if (!string.IsNullOrEmpty(definition.unlockRegionId) &&
                !SaveManager.Current.unlockedRegionIds.Contains(definition.unlockRegionId))
            {
                SaveManager.Current.unlockedRegionIds.Add(definition.unlockRegionId);
                changed = true;
            }
            return changed;
        }
    }
}
