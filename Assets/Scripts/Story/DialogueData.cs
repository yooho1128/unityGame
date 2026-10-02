using System;
using System.Collections.Generic;

namespace ShadowTheater.Story
{
    /// <summary>
    /// Resources/Data/DialogueCatalog.json의 직렬화 구조.
    /// Ink/Yarn을 도입하기 전에도 대사를 바로 제작할 수 있고, dialogueId는 추후 knot 이름으로 그대로 쓴다.
    /// </summary>
    [Serializable]
    public class DialogueCatalogData
    {
        public List<DialogueSequence> dialogues = new List<DialogueSequence>();
    }

    [Serializable]
    public class DialogueSequence
    {
        public string dialogueId;
        public List<DialogueLine> lines = new List<DialogueLine>();
        public List<DialogueChoice> choices = new List<DialogueChoice>();
    }

    [Serializable]
    public class DialogueLine
    {
        public string speaker;
        public string text;
        public string emotion;
        public string requiredFlag;
        public int requiredFlagValue = 1;
        public string blockedFlag;
        public string setFlag;
        public int setFlagValue = 1;
    }

    [Serializable]
    public class DialogueChoice
    {
        public string text;
        public string nextDialogueId;
        public string requiredFlag;
        public int requiredFlagValue = 1;
        public string blockedFlag;
        public string setFlag;
        public int setFlagValue = 1;
        public string startQuestId;
    }
}
