using ShadowTheater.Field;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Story
{
    /// <summary>최종 무대 오브젝트. 마지막 선택 대사 후 누적 선택에 맞는 엔딩을 재생한다.</summary>
    public class EndingTrigger : MonoBehaviour, IInteractable
    {
        [SerializeField] private string requiredFlag = "story_finale_unlocked";
        [SerializeField] private string decisionDialogueId = "finale_director_choice";
        [SerializeField] private Color decisionAccent = new Color(0.74f, 0.56f, 1f, 1f);
        [SerializeField] private bool allowReplay;

        private bool _busy;

        public void Interact(PlayerController player)
        {
            if (_busy || (!string.IsNullOrEmpty(requiredFlag) && !SaveManager.HasFlag(requiredFlag))) return;
            if (!allowReplay && !string.IsNullOrEmpty(SaveManager.Current?.lastEndingId)) return;
            if (DialogueController.Instance == null || DialogueController.Instance.IsPlaying ||
                EndingManager.Instance == null) return;

            _busy = true;
            DialogueController.Instance.Play(decisionDialogueId, decisionAccent, () =>
            {
                if (!EndingManager.Instance.PlayResolvedEnding(_ => _busy = false)) _busy = false;
            });
        }
    }
}
