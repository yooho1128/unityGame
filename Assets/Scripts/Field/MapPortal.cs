using ShadowTheater.Save;
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
            if (!string.IsNullOrEmpty(requiredFlag) && !SaveManager.HasFlag(requiredFlag)) return;
            if (!string.IsNullOrEmpty(blockedFlag) && SaveManager.HasFlag(blockedFlag)) return;
            MapLoader.Instance?.TravelTo(targetScene, arrivalCell, arrivalFacing, setCheckpoint);
        }
    }
}
