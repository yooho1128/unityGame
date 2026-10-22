using System.Collections;
using ShadowTheater.Field;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>PC의 키보드·게임패드 포커스와 씬별 플랫폼 표시를 관리한다.</summary>
    [DisallowMultipleComponent]
    public sealed class DesktopPlatformController : MonoBehaviour
    {
        private const float NavigationThreshold = .55f;
        private float _nextSelectionAt;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (!Application.isMobilePlatform) Application.runInBackground = true;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(ApplyAfterFrame());
        }

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(ApplyAfterFrame());

        private IEnumerator ApplyAfterFrame()
        {
            yield return null;
            foreach (PlatformDisplayGroup group in FindObjectsOfType<PlatformDisplayGroup>(true)) group.Apply();
            if (!Application.isMobilePlatform)
            {
                HideVirtualButtons(FindObjectsOfType<VirtualDPadButton>(true));
                HideVirtualButtons(FindObjectsOfType<VirtualActionButton>(true));
                HideVirtualButtons(FindObjectsOfType<VirtualPauseButton>(true));
                HideVirtualButtons(FindObjectsOfType<VirtualPartyButton>(true));
                HideVirtualButtons(FindObjectsOfType<VirtualScriptBookButton>(true));
                HideVirtualButtons(FindObjectsOfType<VirtualWorldMapButton>(true));
                HideVirtualButtons(FindObjectsOfType<VirtualShopButton>(true));
                SelectFirstUsable();
            }
        }

        private static void HideVirtualButtons<T>(T[] controls) where T : Component
        {
            foreach (T control in controls)
                if (control != null) control.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Application.isMobilePlatform || EventSystem.current == null) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            bool navigation = Mathf.Abs(Input.GetAxisRaw("Horizontal")) >= NavigationThreshold ||
                              Mathf.Abs(Input.GetAxisRaw("Vertical")) >= NavigationThreshold ||
                              Input.GetKeyDown(KeyCode.Tab);
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (navigation && (selected == null || !selected.activeInHierarchy) &&
                Time.unscaledTime >= _nextSelectionAt)
            {
                SelectFirstUsable();
                _nextSelectionAt = Time.unscaledTime + .18f;
            }
#endif
        }

        public static void SelectFirstUsable(GameObject scope = null)
        {
            if (Application.isMobilePlatform || EventSystem.current == null) return;
            Selectable[] candidates = scope != null
                ? scope.GetComponentsInChildren<Selectable>(true)
                : FindObjectsOfType<Selectable>(true);
            foreach (Selectable selectable in candidates)
            {
                if (selectable == null || !selectable.IsActive() || !selectable.IsInteractable() ||
                    !selectable.gameObject.activeInHierarchy) continue;
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
                return;
            }
        }
    }
}
