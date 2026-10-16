using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowTheater.Data;
using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShadowTheater.Story
{
    public class TownRumorNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private string regionId;
        [SerializeField] private string dialogueId;
        [SerializeField] private Color accent = new Color(.72f, .62f, 1f, 1f);

        private void Start() => ApplyResolvedVisual();

        public void Interact(PlayerController player)
        {
            RegionData current = RegionRepository.GetByScene(SaveManager.Current?.mapId);
            if (current == null || current.regionId != regionId || !current.isSettlement) return;
            DialogueController dialogue = DialogueController.Instance;
            if (dialogue == null || dialogue.IsPlaying) return;
            bool resolved = TownServicePolicy.IsRumorResolved(regionId);
            string selectedDialogue = resolved ? TownServicePolicy.RumorFollowupDialogueId(regionId) : dialogueId;
            dialogue.Play(selectedDialogue, resolved ? Color.Lerp(accent, Color.white, .35f) : accent, () =>
            {
                if (resolved)
                {
                    if (TownRestorationService.ClaimReturnVisit(regionId, out int stage))
                        SaveFeedbackController.Instance?.ShowMessage($"마을 복구가 [{TownRestorationService.StageLabel(stage)}] 단계에 도달했습니다.");
                    SaveManager.Instance?.Save();
                    return;
                }
                SaveManager.SetFlag("heard_rumor_" + regionId);
                string questId = TownServicePolicy.RumorQuestId(regionId);
                if (!string.IsNullOrEmpty(questId)) QuestManager.Instance?.TryStartQuest(questId, false);
                SaveManager.Instance?.Save();
            });
        }

        private void ApplyResolvedVisual()
        {
            if (!TownServicePolicy.IsRumorResolved(regionId)) return;
            SpriteRenderer actor = GetComponent<SpriteRenderer>();
            if (actor != null) actor.color = Color.Lerp(actor.color, accent, .45f);
            transform.localScale *= 1.08f;
        }
    }
}
