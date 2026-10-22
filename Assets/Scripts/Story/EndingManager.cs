using System;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>누적된 선택 플래그를 비교하여 우선순위가 가장 높은 엔딩을 결정하고 재생한다.</summary>
    public class EndingManager : MonoBehaviour
    {
        public static EndingManager Instance { get; private set; }
        public static event Action<EndingDefinition> EndingUnlocked;

        [SerializeField] private Color endingAccent = new Color(0.78f, 0.66f, 1f, 1f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public EndingDefinition ResolveEnding()
        {
            EndingDefinition best = null;
            foreach (var ending in EndingRepository.All)
            {
                if (ending == null || !Matches(ending)) continue;
                if (best == null || ending.priority > best.priority) best = ending;
            }
            return best;
        }

        public bool PlayResolvedEnding(Action<EndingDefinition> onComplete = null, Action onCancelled = null)
        {
            var ending = ResolveEnding();
            if (ending == null)
            {
                Debug.LogWarning("[Ending] 현재 선택에 맞는 엔딩이 없습니다.");
                return false;
            }

            var dialogue = DialogueController.Instance;
            if (dialogue == null || dialogue.IsPlaying) return false;
            bool started = dialogue.Play(ending.dialogueId, endingAccent, () =>
            {
                Unlock(ending);
                onComplete?.Invoke(ending);
            }, onCancelled);
            if (started) AdaptiveMusicDirector.Instance?.PlayEnding();
            return started;
        }

        private static bool Matches(EndingDefinition ending)
        {
            if (ending.requirements == null) return true;
            foreach (var requirement in ending.requirements)
            {
                if (requirement == null || string.IsNullOrEmpty(requirement.flag)) continue;
                int value = SaveManager.GetFlag(requirement.flag);
                if (value < requirement.minValue) return false;
                if (requirement.maxValue != 0 && value > requirement.maxValue) return false;
            }
            return true;
        }

        private static void Unlock(EndingDefinition ending)
        {
            var save = SaveManager.Current;
            if (save == null) return;
            save.lastEndingId = ending.endingId;
            if (!save.cycleCompleted)
            {
                save.cycleCompleted = true;
                save.completedCycles++;
            }
            if (!save.unlockedEndingIds.Contains(ending.endingId))
                save.unlockedEndingIds.Add(ending.endingId);
            SaveManager.SetFlag("ending_archive_count", save.unlockedEndingIds.Count);
            SaveManager.SetFlag($"ending_{ending.endingId}_seen");
            save.endingHistory ??= new System.Collections.Generic.List<EndingRecordData>();
            string epilogue = EndingEpilogueService.Build(ending);
            EndingRecordData record = save.endingHistory.Find(x => x.endingId == ending.endingId && x.cycle == save.cycle);
            if (record == null)
            {
                record = new EndingRecordData { endingId = ending.endingId, cycle = save.cycle };
                save.endingHistory.Add(record);
            }
            record.companionShadowId = CompanionBondService.CurrentCompanionShadowId();
            record.epilogue = epilogue;
            record.memoryScore = SaveManager.GetFlag("choice_memory");
            record.compassionScore = SaveManager.GetFlag("choice_compassion");
            record.controlScore = SaveManager.GetFlag("choice_control");
            SaveManager.Instance.Save();
            SaveFeedbackController.Instance?.ShowMessage(epilogue);
            EndingUnlocked?.Invoke(ending);
        }
    }
}
