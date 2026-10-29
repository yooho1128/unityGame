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
        [Tooltip("비워 두면 일반 포털. 지정하면 가방 도구나 기록한 그림자 권능이 필요")]
        [SerializeField] private TraversalAbility requiredTraversalAbilities;

        private float _nextFeedbackAt;
        public string DestinationScene => targetScene;
        public bool CanGuideTravel => RegionProgress.MeetsTravelRequirement(targetScene, requiredFlag) &&
            (string.IsNullOrEmpty(blockedFlag) || !SaveManager.HasFlag(blockedFlag)) &&
            (requiredTraversalAbilities == TraversalAbility.None || TraversalAbilityService.TryResolve(requiredTraversalAbilities,
                SaveManager.Current, ShadowTheater.Data.ShadowDatabase.Instance, out _, out _));
        public string DestinationName
        {
            get
            {
                var region = RegionRepository.GetByScene(targetScene);
                if (region == null || RegionProgress.GetKnowledge(region) == RegionMapKnowledge.Hidden)
                    return L10n.Get("portal.unknown_destination", "알 수 없는 길");
                return L10n.Text(region.displayName);
            }
        }

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
            TryTravel();
        }

        private void TryTravel()
        {
            if (!RegionProgress.MeetsTravelRequirement(targetScene, requiredFlag))
            {
                ShowLockedFeedback();
                return;
            }
            if (!string.IsNullOrEmpty(blockedFlag) && SaveManager.HasFlag(blockedFlag))
            {
                ShowFeedback(L10n.Get("portal.blocked", "지금은 이 길을 이용할 수 없습니다."));
                return;
            }
            if (requiredTraversalAbilities != TraversalAbility.None &&
                !TraversalAbilityService.TryResolve(requiredTraversalAbilities, SaveManager.Current,
                    ShadowTheater.Data.ShadowDatabase.Instance, out _, out string traversalMessage))
            {
                ShowFeedback(traversalMessage);
                return;
            }
            if (MapLoader.Instance == null || !MapLoader.Instance.CanTravel) return;
            if (!MapLoader.Instance.TravelTo(targetScene, arrivalCell, arrivalFacing, setCheckpoint))
                ShowLockedFeedback();
        }

        private void ShowLockedFeedback()
        {
            string message = L10n.Get("portal.exploration_gate",
                "아직 관문이 열리지 않았습니다 · 월드맵에서 다른 길을 확인하세요.");
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
