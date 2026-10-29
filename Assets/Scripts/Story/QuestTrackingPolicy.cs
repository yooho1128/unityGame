using System;
using System.Collections.Generic;
using ShadowTheater.Save;

namespace ShadowTheater.Story
{
    /// <summary>Selection is per save: an explicit active quest wins, then main quests, then side quests.</summary>
    public static class QuestTrackingPolicy
    {
        public static QuestDefinition Resolve(IReadOnlyList<QuestDefinition> definitions,
            IReadOnlyList<QuestProgressData> progress, string pinnedId)
        {
            var pinned = Active(definitions, progress, pinnedId);
            if (pinned != null) return pinned;
            QuestDefinition side = null;
            foreach (var p in progress ?? Array.Empty<QuestProgressData>())
            {
                if (p == null || p.completed) continue;
                var q = Find(definitions, p.questId);
                if (q == null) continue;
                if (!q.isSideQuest) return q;
                if (side == null) side = q;
            }
            return side;
        }
        public static QuestDefinition Active(IReadOnlyList<QuestDefinition> definitions,
            IReadOnlyList<QuestProgressData> progress, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var p in progress ?? Array.Empty<QuestProgressData>())
                if (p != null && p.questId == id && !p.completed) return Find(definitions, id);
            return null;
        }
        public static QuestObjectiveDefinition NextObjective(QuestDefinition quest, QuestProgressData progress)
        {
            if (quest == null || progress == null || progress.completed || quest.questId != progress.questId) return null;
            foreach (var objective in quest.objectives ?? new List<QuestObjectiveDefinition>())
            {
                if (objective == null) continue;
                int current = 0;
                foreach (var p in progress.objectives ?? new List<QuestObjectiveProgressData>())
                    if (p != null && p.objectiveId == objective.objectiveId) { current = p.current; break; }
                if (current < Math.Max(1, objective.requiredCount)) return objective;
            }
            return null;
        }
        private static QuestDefinition Find(IReadOnlyList<QuestDefinition> definitions, string id)
        {
            foreach (var q in definitions ?? Array.Empty<QuestDefinition>())
                if (q != null && q.questId == id) return q;
            return null;
        }
    }
}
