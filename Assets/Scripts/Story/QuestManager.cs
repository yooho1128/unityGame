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
        public static event Action<QuestDefinition> GlobalQuestCompleted;

        public QuestDefinition TrackedQuest => FindTrackedQuest();

        private string _initializedSaveGuid;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SaveManager.CurrentChanged += HandleCurrentSaveChanged;
        }

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => SaveManager.Current != null);
            InitializeCurrentSave(false);
        }

        private void OnDestroy()
        {
            SaveManager.CurrentChanged -= HandleCurrentSaveChanged;
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
            foreach (var objective in quest.objectives ?? new List<QuestObjectiveDefinition>())
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

            foreach (var quest in completedThisTick) CompleteQuest(quest, true);
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

        private void CompleteQuest(QuestDefinition quest, bool announce)
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
            if (!string.IsNullOrEmpty(quest.completionFlag)) SaveManager.SetFlag(quest.completionFlag);
            string itemReward = !string.IsNullOrEmpty(quest.rewardItemId) && quest.rewardItemCount > 0
                ? $", {quest.rewardItemId} x{quest.rewardItemCount}" : string.Empty;
            Debug.Log($"[Quest] 완료: {quest.title} (+{quest.rewardGold} 금화{itemReward})");
            if (announce) GlobalQuestCompleted?.Invoke(quest);

            if (!string.IsNullOrEmpty(quest.nextQuestId))
                TryStartQuest(quest.nextQuestId, false);
        }

        private void HandleCurrentSaveChanged()
        {
            if (SaveManager.Current == null)
            {
                _initializedSaveGuid = null;
                return;
            }
            InitializeCurrentSave(true);
        }

        /// <summary>
        /// 구버전/중단 저장에서 이미 발생한 스토리 사실을 퀘스트 진행도로 되살린다.
        /// 완료 퀘스트의 다음 퀘스트가 빠진 경우에도 메인 진행 사슬을 다시 잇는다.
        /// </summary>
        private void InitializeCurrentSave(bool saveRepairs)
        {
            var save = SaveManager.Current;
            if (save == null || save.saveGuid == _initializedSaveGuid) return;
            _initializedSaveGuid = save.saveGuid;

            bool changed = RepairProgressData();
            foreach (var quest in QuestRepository.All)
                if (quest.autoStart && TryStartQuest(quest.questId, false)) changed = true;

            // 완료 복구로 다음 퀘스트가 생길 수 있으므로 사슬 길이만큼 반복할 수 있게 한다.
            bool passChanged;
            int safety = QuestRepository.All.Count + 1;
            do
            {
                passChanged = ReconcileKnownFacts();
                changed |= passChanged;
            } while (passChanged && --safety > 0);

            if (safety <= 0) Debug.LogError("[Quest] 진행 복구 중 퀘스트 연결 순환을 감지했습니다.");
            GlobalQuestChanged?.Invoke();
            if (changed && saveRepairs && SaveManager.Instance != null) SaveManager.Instance.Save(false);
        }

        private bool ReconcileKnownFacts()
        {
            var save = SaveManager.Current;
            if (save == null) return false;
            bool changed = false;

            // 순회 중 CompleteQuest가 다음 진행도를 추가할 수 있으므로 사본을 사용한다.
            foreach (var progress in new List<QuestProgressData>(save.quests))
            {
                if (progress == null || !QuestRepository.TryGet(progress.questId, out var quest)) continue;
                if (progress.completed)
                {
                    changed |= RestoreCompletedQuestState(quest, progress);
                    continue;
                }

                foreach (var objective in quest.objectives ?? new List<QuestObjectiveDefinition>())
                {
                    if (!objective.TryGetType(out var type) || !IsKnownFactSatisfied(type, objective.targetId)) continue;
                    var objectiveProgress = GetOrCreateObjective(progress, objective.objectiveId);
                    int required = Mathf.Max(1, objective.requiredCount);
                    if (objectiveProgress.current >= required) continue;
                    objectiveProgress.current = required;
                    changed = true;
                }

                if (!IsComplete(quest, progress)) continue;
                CompleteQuest(quest, false);
                changed = true;
            }
            return changed;
        }

        private static bool IsKnownFactSatisfied(QuestObjectiveType type, string targetId)
        {
            if (string.IsNullOrEmpty(targetId) || targetId == "*") return false;
            switch (type)
            {
                case QuestObjectiveType.Talk:
                    return SaveManager.HasFlag("talked_" + targetId) || SaveManager.HasFlag(targetId);
                case QuestObjectiveType.Reach:
                {
                    string regionId = targetId.StartsWith("area_", StringComparison.Ordinal)
                        ? targetId.Substring(5) : targetId;
                    var save = SaveManager.Current;
                    return SaveManager.HasFlag("region_" + regionId + "_visited") ||
                           save?.visitedRegionIds?.Contains(regionId) == true;
                }
                case QuestObjectiveType.Record:
                    return SaveManager.IsRecorded(targetId);
                case QuestObjectiveType.Defeat:
                    return SaveManager.IsEncounterCleared(targetId);
                case QuestObjectiveType.Flag:
                    return SaveManager.HasFlag(targetId);
                default:
                    return false;
            }
        }

        private bool RestoreCompletedQuestState(QuestDefinition quest, QuestProgressData progress)
        {
            bool changed = false;
            if (!progress.rewardClaimed)
            {
                SaveManager.AddGold(Mathf.Max(0, quest.rewardGold));
                if (!string.IsNullOrEmpty(quest.rewardItemId) && quest.rewardItemCount > 0)
                    SaveManager.AddItem(quest.rewardItemId, quest.rewardItemCount);
                progress.rewardClaimed = true;
                changed = true;
            }
            string completionKey = $"quest_{quest.questId}_complete";
            if (!SaveManager.HasFlag(completionKey)) { SaveManager.SetFlag(completionKey); changed = true; }
            if (!string.IsNullOrEmpty(quest.completionFlag) && !SaveManager.HasFlag(quest.completionFlag))
            {
                SaveManager.SetFlag(quest.completionFlag);
                changed = true;
            }
            if (!string.IsNullOrEmpty(quest.nextQuestId) && GetProgress(quest.nextQuestId) == null &&
                TryStartQuest(quest.nextQuestId, false)) changed = true;
            return changed;
        }

        private QuestDefinition FindTrackedQuest()
        {
            var save = SaveManager.Current;
            if (save == null) return null;
            foreach (var progress in save.quests)
                if (!progress.completed && QuestRepository.TryGet(progress.questId, out var mainQuest) && !mainQuest.isSideQuest)
                    return mainQuest;
            foreach (var progress in save.quests)
                if (!progress.completed && QuestRepository.TryGet(progress.questId, out var anyQuest)) return anyQuest;
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

        private static bool RepairProgressData()
        {
            var save = SaveManager.Current;
            bool changed = false;
            if (save.quests == null) { save.quests = new List<QuestProgressData>(); changed = true; }
            int removed = save.quests.RemoveAll(x => x == null || string.IsNullOrWhiteSpace(x.questId));
            changed |= removed > 0;
            var unique = new Dictionary<string, QuestProgressData>(StringComparer.Ordinal);
            for (int i = 0; i < save.quests.Count; i++)
            {
                var progress = save.quests[i];
                if (!unique.TryGetValue(progress.questId, out var existing))
                {
                    unique.Add(progress.questId, progress);
                    continue;
                }
                MergeProgress(existing, progress);
                save.quests.RemoveAt(i);
                i--;
                changed = true;
            }
            foreach (var progress in save.quests)
            {
                if (progress.objectives == null) { progress.objectives = new List<QuestObjectiveProgressData>(); changed = true; }
                removed = progress.objectives.RemoveAll(x => x == null || string.IsNullOrWhiteSpace(x.objectiveId));
                changed |= removed > 0;
                var objectiveMap = new Dictionary<string, QuestObjectiveProgressData>(StringComparer.Ordinal);
                for (int i = 0; i < progress.objectives.Count; i++)
                {
                    var p = progress.objectives[i];
                    if (!objectiveMap.TryGetValue(p.objectiveId, out var existing))
                    {
                        objectiveMap.Add(p.objectiveId, p);
                        continue;
                    }
                    existing.current = Mathf.Max(existing.current, p.current);
                    progress.objectives.RemoveAt(i);
                    i--;
                    changed = true;
                }
                if (!QuestRepository.TryGet(progress.questId, out var quest)) continue;
                foreach (var objective in quest.objectives ?? new List<QuestObjectiveDefinition>())
                {
                    var p = progress.objectives.Find(x => x != null && x.objectiveId == objective.objectiveId);
                    if (p == null)
                    {
                        progress.objectives.Add(new QuestObjectiveProgressData { objectiveId = objective.objectiveId });
                        changed = true;
                    }
                    else
                    {
                        int clamped = Mathf.Clamp(p.current, 0, Mathf.Max(1, objective.requiredCount));
                        if (clamped != p.current) { p.current = clamped; changed = true; }
                    }
                }
            }
            return changed;
        }

        private static void MergeProgress(QuestProgressData destination, QuestProgressData source)
        {
            destination.completed |= source.completed;
            destination.rewardClaimed |= source.rewardClaimed;
            destination.objectives ??= new List<QuestObjectiveProgressData>();
            foreach (var sourceObjective in source.objectives ?? new List<QuestObjectiveProgressData>())
            {
                if (sourceObjective == null || string.IsNullOrWhiteSpace(sourceObjective.objectiveId)) continue;
                var target = destination.objectives.Find(x => x != null && x.objectiveId == sourceObjective.objectiveId);
                if (target == null)
                    destination.objectives.Add(new QuestObjectiveProgressData
                        { objectiveId = sourceObjective.objectiveId, current = sourceObjective.current });
                else target.current = Mathf.Max(target.current, sourceObjective.current);
            }
        }
    }
}
