using System;
using System.Collections.Generic;

namespace ShadowTheater.Story
{
    [Serializable]
    public class CompanionEventCatalogData
    {
        public List<CompanionEventDefinition> events = new List<CompanionEventDefinition>();
    }

    /// <summary>특정 지역을 동행 그림자와 탐색할 때 발견하는 일회성 비밀 장면.</summary>
    [Serializable]
    public class CompanionEventDefinition
    {
        public string eventId;
        public string regionId;
        public string requiredShadowId;
        public string requiredFlag;
        public string blockedFlag;
        public int minSteps = 5;
        public int guaranteedSteps = 18;
        public float chancePerStep = .12f;
        public string dialogueId;
        public string accentHex = "BFA7FF";
        public int rewardGold;
        public string rewardFlag;
        public string rewardItemId;
        public int rewardItemCount = 1;
        public string unlockRegionId;
        public string rewardMessage;

        public string CompletionFlag => "companion_event_" + eventId + "_complete";
    }
}
