using System;
using System.Collections.Generic;

namespace ShadowTheater.Story
{
    public enum QuestObjectiveType
    {
        Talk,
        Reach,
        Record,
        Defeat,
        Flag
    }

    [Serializable]
    public class QuestCatalogData
    {
        public List<QuestDefinition> quests = new List<QuestDefinition>();
    }

    [Serializable]
    public class QuestDefinition
    {
        public string questId;
        public string title;
        public string summary;
        public string prerequisiteQuestId;
        public string nextQuestId;
        public bool autoStart;
        public bool isSideQuest;
        public string completionFlag;
        public int rewardGold;
        public string rewardItemId;
        public int rewardItemCount;
        public List<QuestObjectiveDefinition> objectives = new List<QuestObjectiveDefinition>();
    }

    [Serializable]
    public class QuestObjectiveDefinition
    {
        public string objectiveId;
        public string type;
        public string targetId;
        public string description;
        public int requiredCount = 1;

        public bool TryGetType(out QuestObjectiveType objectiveType) =>
            Enum.TryParse(type, true, out objectiveType);
    }
}
