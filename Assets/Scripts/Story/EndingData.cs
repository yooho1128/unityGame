using System;
using System.Collections.Generic;

namespace ShadowTheater.Story
{
    [Serializable]
    public class EndingCatalogData
    {
        public List<EndingDefinition> endings = new List<EndingDefinition>();
    }

    [Serializable]
    public class EndingDefinition
    {
        public string endingId;
        public string title;
        public string subtitle;
        public string dialogueId;
        public int priority;
        public List<EndingFlagRequirement> requirements = new List<EndingFlagRequirement>();
    }

    [Serializable]
    public class EndingFlagRequirement
    {
        public string flag;
        public int minValue;
        /// <summary>0이면 상한 없음.</summary>
        public int maxValue;
    }
}
