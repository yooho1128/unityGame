using System;
using System.Collections;
using System.Collections.Generic;
using ShadowTheater.Save;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>
    /// 대화/지역 도달/그림자 기록/전투 승리 이벤트를 현재 퀘스트 목표에 반영한다.
    /// 진행도는 SaveData에 직접 기록되어 씬 전환과 앱 재실행에도 유지된다.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }
        public static event Action GlobalQuestChanged;

        public QuestDefinition TrackedQuest => FindTrackedQuest();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => SaveManager.Current != null);
            RepairProgressData();

            foreach (var quest in QuestRepository.All)
                if (quest.autoStart) TryStartQuest(quest.questId, false);

            GlobalQuestChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool TryStartQuest(string questId, bool saveImmediately = true)
        {
            var save = SaveManager.Current;
            if (save == null || !QuestRepository.TryGet(questId, out var quest)) return false;
            if (save.quests.Exists(x => x.questId == questId)) return false;

            if (!string.IsNullOrEmpty(quest.prerequisiteQuestId))
            {
                var prerequisite = save.quests.Find(x => x.questId == quest.prerequisiteQuestId);
                if (prerequisite == null || !prerequisite.completed) return false;
            }

            var progress = new QuestProgressData { questId = quest.questId };
            foreach (var objective in quest.objectives)
                progress.objectives.Add(new QuestObjectiveProgressData { objectiveId = objective.objectiveId });
            save.quests.Add(progress);

            Debug.Log($"[Quest] 시작: {quest.title}");
            GlobalQuestChanged?.Invoke();
            if (saveImmediately) SaveManager.Instance.Save();
            return true;
        }

        public void Notify(QuestObjectiveType type, string targetId, int amount = 1)
        {
            var save = SaveManager.Current;
            if (save == null || amount <= 0) return;

            bool changed = false;
            var completedThisTick = new List<QuestDefinition>();
            foreach (var progress in save.quests)
            {
                if (progress.completed || !QuestRepository.TryGet(progress.questId, out var quest)) continue;

                for (int i = 0; i < quest.objectives.Count; i++)
                {
                    var objective = quest.objectives[i];
                    if (!objective.TryGetType(out var objectiveType) || objectiveType != type) continue;
                    if (objective.targetId != "*" && objective.targetId != targetId) continue;

                    var objectiveProgress = GetOrCreateObjective(progress, objective.objectiveId);
                    int required = Mathf.Max(1, objective.requiredCount);
                    if (objectiveProgress.current >= required) continue;
                    objectiveProgress.current = Mathf.Min(required, objectiveProgress.current + amount);
                    changed = true;
                }

                if (IsComplete(quest, progress)) completedThisTick.Add(quest);
            }

            foreach (var quest in completedThisTick) CompleteQuest(quest);
            if (!changed && completedThisTick.Count == 0) return;

            GlobalQuestChanged?.Invoke();
            SaveManager.Instance.Save();
        }

        public QuestProgressData GetProgress(string questId) =>
            SaveManager.Current?.quests.Find(x => x.questId == questId);

        public QuestObjectiveProgressData GetObjectiveProgress(string questId, string objectiveId)
        {
            var quest = GetProgress(questId);
            return quest?.objectives.Find(x => x.objectiveId == objectiveId);
        }

        private void CompleteQuest(QuestDefinition quest)
        {
            var progress = GetProgress(quest.questId);
            if (progress == null || progress.completed) return;

            progress.completed = true;
            if (!progress.rewardClaimed)
            {
                SaveManager.AddGold(Mathf.Max(0, quest.rewardGold));
                if (!string.IsNullOrEmpty(quest.rewardItemId) && quest.rewardItemCount > 0)
                    SaveManager.AddItem(quest.rewardItemId, quest.rewardItemCount);
                progress.rewardClaimed = true;
            }
            SaveManager.SetFlag($"quest_{quest.questId}_complete");
            string itemReward = !string.IsNullOrEmpty(quest.rewardItemId) && quest.rewardItemCount > 0
                ? $", {quest.rewardItemId} x{quest.rewardItemCount}" : string.Empty;
            Debug.Log($"[Quest] 완료: {quest.title} (+{quest.rewardGold} 금화{itemReward})");

            if (!string.IsNullOrEmpty(quest.nextQuestId))
                TryStartQuest(quest.nextQuestId, false);
        }

        private QuestDefinition FindTrackedQuest()
        {
            var save = SaveManager.Current;
            if (save == null) return null;
            foreach (var progress in save.quests)
                if (!progress.completed && QuestRepository.TryGet(progress.questId, out var quest)) return quest;
            return null;
        }

        private static QuestObjectiveProgressData GetOrCreateObjective(QuestProgressData progress, string objectiveId)
        {
            var item = progress.objectives.Find(x => x.objectiveId == objectiveId);
            if (item != null) return item;
            item = new QuestObjectiveProgressData { objectiveId = objectiveId };
            progress.objectives.Add(item);
            return item;
        }

        private static bool IsComplete(QuestDefinition quest, QuestProgressData progress)
        {
            if (quest.objectives == null || quest.objectives.Count == 0) return false;
            foreach (var objective in quest.objectives)
            {
                var p = progress.objectives.Find(x => x.objectiveId == objective.objectiveId);
                if (p == null || p.current < Mathf.Max(1, objective.requiredCount)) return false;
            }
            return true;
        }

        private static void RepairProgressData()
        {
            var save = SaveManager.Current;
            save.quests ??= new List<QuestProgressData>();
            foreach (var progress in save.quests)
                progress.objectives ??= new List<QuestObjectiveProgressData>();
        }
    }
}
