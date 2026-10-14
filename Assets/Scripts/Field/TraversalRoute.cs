using System.Collections;
using ShadowTheater.Data;
using ShadowTheater.Save;
using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>같은 지역 안의 물길·절벽·전용 도로를 도구 또는 그림자 권능으로 건너는 범용 경로.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class TraversalRoute : MonoBehaviour, IInteractable
    {
        [SerializeField] private TraversalAbility allowedAbilities = TraversalAbility.PortableBoat;
        [SerializeField] private Vector2Int destinationCell;
        [SerializeField] private FacingDir destinationFacing = FacingDir.Right;

        private bool _busy;
        private float _nextFeedbackAt;

        public TraversalAbility AllowedAbilities => allowedAbilities;
        public Vector2Int DestinationCell => destinationCell;

        private void Reset() => GetComponent<Collider2D>().isTrigger = true;

        public void Interact(PlayerController player)
        {
            if (_busy || player == null || player.IsLocked || player.IsMoving) return;
            if (!TraversalAbilityService.TryResolve(allowedAbilities, SaveManager.Current,
                    ShadowDatabase.Instance, out TraversalAbility ability, out string message))
            {
                ShowFeedback(message);
                return;
            }
            StartCoroutine(Traverse(player, ability, message));
        }

        private IEnumerator Traverse(PlayerController player, TraversalAbility ability, string message)
        {
            _busy = true;
            player.MoveInput = Vector2.zero;
            player.Lock();
            var fader = ScreenFader.Instance;
            if (fader != null) yield return fader.FadeOut();

            bool moved = player.SnapToSafeCell(destinationCell, destinationCell, out _);
            if (moved)
            {
                player.SetFacing(destinationFacing);
                SaveManager.Instance?.Save();
            }

            if (fader != null) yield return fader.FadeIn();
            player.Unlock();
            _busy = false;
            ShowFeedback(moved ? message : "안전하게 도착할 수 있는 지점을 찾지 못했습니다.");
            if (moved) Debug.Log($"[Traversal] {ability}: {name} → {destinationCell}");
        }

        private void ShowFeedback(string message)
        {
            if (Time.unscaledTime < _nextFeedbackAt) return;
            _nextFeedbackAt = Time.unscaledTime + 1.25f;
            SaveFeedbackController.Instance?.ShowMessage(message);
        }
    }
}
