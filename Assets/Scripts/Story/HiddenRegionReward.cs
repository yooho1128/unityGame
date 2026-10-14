using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>숨겨진 지역 퍼즐 뒤에 놓이는 일회성 조사 보상.</summary>
    public class HiddenRegionReward : MonoBehaviour, IInteractable
    {
        [SerializeField] private string rewardId;
        [SerializeField] private string dialogueId;
        [SerializeField] private string requiredEncounterId;
        [SerializeField] private string rewardItemId;
        [SerializeField, Min(0)] private int rewardItemCount;
        [SerializeField, Min(0)] private int rewardGold;
        [SerializeField] private string rewardMessage = "숨겨진 유품을 발견했다";
        [SerializeField] private Color accent = new Color(.8f, .65f, .35f, 1f);

        public string CompletionFlag => "hidden_reward_" + rewardId + "_claimed";
        private bool _claiming;

        private void Start()
        {
            if (!string.IsNullOrEmpty(rewardId) && SaveManager.HasFlag(CompletionFlag))
                gameObject.SetActive(false);
        }

        public void Interact(PlayerController player)
        {
            if (_claiming || string.IsNullOrEmpty(rewardId) || SaveManager.HasFlag(CompletionFlag)) return;
            if (!string.IsNullOrEmpty(requiredEncounterId) && !SaveManager.IsEncounterCleared(requiredEncounterId))
            {
                SaveFeedbackController.Instance?.ShowMessage("기억을 지키는 그림자를 먼저 진정시켜야 한다");
                return;
            }
            DialogueController dialogue = DialogueController.Instance;
            if (dialogue != null && dialogue.IsPlaying) return;
            _claiming = true;
            if (dialogue == null || string.IsNullOrEmpty(dialogueId)) { Claim(); return; }
            bool started = dialogue.Play(dialogueId, accent, Claim);
            if (!started && !SaveManager.HasFlag(CompletionFlag)) _claiming = false;
        }

        private void Claim()
        {
            if (SaveManager.HasFlag(CompletionFlag)) { gameObject.SetActive(false); return; }
            if (!string.IsNullOrEmpty(rewardItemId) && rewardItemCount > 0)
                SaveManager.AddItem(rewardItemId, rewardItemCount);
            if (rewardGold > 0) SaveManager.AddGold(rewardGold);
            SaveManager.SetFlag(CompletionFlag);
            if (SaveManager.Instance != null && SaveManager.Current != null) SaveManager.Instance.Save();
            SaveFeedbackController.Instance?.ShowMessage(rewardMessage);
            gameObject.SetActive(false);
        }
    }
}
