using ShadowTheater.Save;
using ShadowTheater.Story;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>맵 가장자리 출구나 문. 접촉 또는 A 버튼으로 다른 씬의 지정 타일로 이동한다.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class MapPortal : MonoBehaviour, IInteractable
    {
        [SerializeField] private string targetScene = "MoonlitMeadow";
        [SerializeField] private Vector2Int arrivalCell;
        [SerializeField] private FacingDir arrivalFacing = FacingDir.Down;
        [SerializeField] private bool activateOnTouch = true;
        [SerializeField] private bool setCheckpoint;
        [Header("선택 조건")]
        [SerializeField] private string requiredFlag;
        [SerializeField] private string blockedFlag;

        private float _nextFeedbackAt;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = activateOnTouch;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!activateOnTouch || !other.TryGetComponent<PlayerController>(out _)) return;
            TryTravel();
        }

        public void Interact(PlayerController player)
        {
            if (!activateOnTouch) TryTravel();
        }

        private void TryTravel()
        {
            if (!string.IsNullOrEmpty(requiredFlag) && !SaveManager.HasFlag(requiredFlag))
            {
                ShowLockedFeedback();
                return;
            }
            if (!string.IsNullOrEmpty(blockedFlag) && SaveManager.HasFlag(blockedFlag))
            {
                ShowFeedback(L10n.Get("portal.blocked", "지금은 이 길을 이용할 수 없습니다."));
                return;
            }
            if (MapLoader.Instance == null || !MapLoader.Instance.CanTravel) return;
            if (!MapLoader.Instance.TravelTo(targetScene, arrivalCell, arrivalFacing, setCheckpoint))
                ShowLockedFeedback();
        }

        private void ShowLockedFeedback()
        {
            var quest = QuestManager.Instance?.TrackedQuest;
            string message = quest != null
                ? L10n.Format("portal.locked_quest", "아직 길이 열리지 않았습니다 · 현재 기억: {0}", L10n.Text(quest.title))
                : L10n.Get("portal.locked", "아직 이 길로 나아갈 수 없습니다.");
            ShowFeedback(message);
        }

        private void ShowFeedback(string message)
        {
            if (Time.unscaledTime < _nextFeedbackAt) return;
            _nextFeedbackAt = Time.unscaledTime + 1.25f;
            SaveFeedbackController.Instance?.ShowMessage(message);
        }
    }
}
