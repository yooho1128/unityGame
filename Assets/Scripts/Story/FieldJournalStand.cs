using ShadowTheater.Field;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    public sealed class FieldJournalStand:MonoBehaviour,IInteractable
    {
        public void Interact(PlayerController player)=>
            SaveFeedbackController.Instance?.ShowMessage(ExplorationJournalService.Summary());
    }
}
