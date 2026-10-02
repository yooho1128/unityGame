using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>필드 NPC. 첫 대화/반복 대사를 구분하고 완료 플래그를 저장한다.</summary>
    public class StoryNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private string npcId = "npc_aria";
        [SerializeField] private string firstDialogueId = "npc_aria_intro";
        [SerializeField] private string repeatDialogueId = "npc_aria_repeat";
        [SerializeField] private string completionFlag = "talked_npc_aria";
        [SerializeField] private Color dialogueAccent = new Color(0.71f, 0.38f, 1f, 1f);

        public void Interact(PlayerController player)
        {
            var dialogue = DialogueController.Instance;
            if (dialogue == null)
            {
                Debug.LogWarning($"[StoryNpc:{npcId}] 씬에 DialogueController가 없습니다.");
                return;
            }
            if (dialogue.IsPlaying) return;

            bool alreadyTalked = !string.IsNullOrEmpty(completionFlag) && SaveManager.HasFlag(completionFlag);
            string dialogueId = alreadyTalked && !string.IsNullOrEmpty(repeatDialogueId)
                ? repeatDialogueId
                : firstDialogueId;

            dialogue.Play(dialogueId, dialogueAccent, () =>
            {
                if (!alreadyTalked && !string.IsNullOrEmpty(completionFlag))
                    SaveManager.SetFlag(completionFlag);
                QuestManager.Instance?.Notify(QuestObjectiveType.Talk, npcId);
                if (SaveManager.Instance != null && SaveManager.Current != null)
                    SaveManager.Instance.Save();
            });
        }
    }
}
