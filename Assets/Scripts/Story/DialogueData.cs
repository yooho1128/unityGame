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
    }

    [Serializable]
    public class DialogueLine
    {
        public string speaker;
        public string text;
        public string emotion;
    }
}
