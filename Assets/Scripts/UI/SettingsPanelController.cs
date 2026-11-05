using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class SettingsPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private GameObject titleRoot;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider ambienceSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Text masterValue;
        [SerializeField] private Text ambienceValue;
        [SerializeField] private Text musicValue;
        [SerializeField] private Text sfxValue;
        [SerializeField] private Text vibrationValue;
        [SerializeField] private Text textSpeedValue;
        [SerializeField] private Text languageValue;
        [SerializeField] private GameObject vibrationRow;
        [SerializeField] private GameObject desktopDisplayRoot;
        [SerializeField] private Text screenModeValue;
        [SerializeField] private Text resolutionValue;
        [SerializeField] private Text vSyncValue;
        private bool _refreshing;
        private SettingsPanelView _presentation;
        private Font _runtimeFont;
        private GameObject _returnSelection;
        private int _closedFrame = -1;
        private bool _runtimeConfigured;
        public bool IsOpen => root != null && root.activeInHierarchy;
        public bool BlocksParentInput => IsOpen || _closedFrame == Time.frameCount;

        public static SettingsPanelController CreateForMenu(Transform parent, GameObject returnPanel, Font font)
        {
            var panel = RuntimeUiControls.Rect(parent, "FullSettings", Vector2.zero, Vector2.one);
            panel.gameObject.AddComponent<Image>().color = TheatreUiStyle.Midnight;
            var controller = panel.gameObject.AddComponent<SettingsPanelController>();
            controller.root = panel.gameObject;
            controller.titleRoot = returnPanel;
            controller._runtimeFont = font;
            controller._runtimeConfigured = true;
            panel.gameObject.SetActive(false);
            return controller;
        }

        private void Awake()
        {
            // Runtime panels can be created under an inactive pause menu; their first Awake may occur on Open.
            if (_runtimeConfigured) return;
            if (root != null) root.SetActive(false);
            if (vibrationRow != null) vibrationRow.SetActive(Application.isMobilePlatform);
            if (desktopDisplayRoot != null) desktopDisplayRoot.SetActive(!Application.isMobilePlatform);
        }

        public void Open()
        {
            if (IsOpen || root == null) return;
            _returnSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (titleRoot != null) titleRoot.SetActive(false);
            if (root != null) root.SetActive(true);
            if (_presentation == null && root != null)
            {
                var label = root.GetComponentInChildren<Text>(true);
                var font = _runtimeFont != null ? _runtimeFont : label != null ? label.font : null;
                if (font != null)
                {
                    _presentation = root.GetComponent<SettingsPanelView>() ?? root.AddComponent<SettingsPanelView>();
                    _presentation.Build(this, font);
                }
            }
            Refresh();
            if (_presentation != null) _presentation.FocusCurrentTab();
            else DesktopPlatformController.SelectFirstUsable(root);
        }

        public void Close()
        {
            CloseInternal(true);
        }

        public void DismissWithParent() => CloseInternal(false);

        private void CloseInternal(bool restoreFocus)
        {
            if (root == null || !root.activeSelf) return;
            _closedFrame = Time.frameCount;
            if (root != null) root.SetActive(false);
            if (titleRoot != null) titleRoot.SetActive(true);
            if (!restoreFocus) return;
            var previous = _returnSelection != null ? _returnSelection.GetComponent<Selectable>() : null;
            if (EventSystem.current != null && previous != null && previous.IsActive() && previous.IsInteractable())
                EventSystem.current.SetSelectedGameObject(_returnSelection);
            else DesktopPlatformController.SelectFirstUsable(titleRoot);
            _returnSelection = null;
        }

        public void SetMaster(float value)
        {
            if (_refreshing || GameSettings.Instance == null) return;
            GameSettings.Instance.SetMasterVolume(value); RefreshLabels();
        }

        public void SetAmbience(float value)
        {
            if (_refreshing || GameSettings.Instance == null) return;
            GameSettings.Instance.SetAmbienceVolume(value); RefreshLabels();
        }

        public void SetMusic(float value)
        {
            if (_refreshing || GameSettings.Instance == null) return;
            GameSettings.Instance.SetMusicVolume(value); RefreshLabels();
        }

        public void SetSfx(float value)
        {
            if (_refreshing || GameSettings.Instance == null) return;
            GameSettings.Instance.SetSfxVolume(value); RefreshLabels();
        }

        public void ToggleVibration()
        {
            if (GameSettings.Instance == null) return;
            GameSettings.Instance.SetVibration(!GameSettings.Vibration);
            if (GameSettings.Vibration) GameSettings.TryVibrate();
            RefreshLabels();
        }

        public void CycleTextSpeed()
        {
            if (GameSettings.Instance == null) return;
            float next = GameSettings.TextSpeed < 33f ? 42f : GameSettings.TextSpeed < 60f ? 72f : 24f;
            GameSettings.Instance.SetTextSpeed(next); RefreshLabels();
        }

        public void ToggleLanguage()
        {
            if (GameSettings.Instance == null) return;
            if (!GameSettings.EnglishEnabled)
            {
                GameSettings.Instance.SetLanguage(GameLanguage.Korean);
                RefreshLabels();
                return;
            }
            GameSettings.Instance.SetLanguage(GameSettings.Language == GameLanguage.Korean
                ? GameLanguage.English : GameLanguage.Korean);
            RefreshLabels();
        }

        public void ToggleFullscreen()
        {
            GameSettings.Instance?.ToggleFullscreen();
            RefreshLabels();
        }

        public void CycleResolution()
        {
            GameSettings.Instance?.CycleResolution();
            RefreshLabels();
        }

        public void ToggleVSync()
        {
            GameSettings.Instance?.ToggleVSync();
            RefreshLabels();
        }

        private void Refresh()
        {
            _refreshing = true;
            if (masterSlider != null) masterSlider.value = GameSettings.MasterVolume;
            if (ambienceSlider != null) ambienceSlider.value = GameSettings.AmbienceVolume;
            if (musicSlider != null) musicSlider.value = GameSettings.MusicVolume;
            if (sfxSlider != null) sfxSlider.value = GameSettings.SfxVolume;
            _refreshing = false;
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (masterValue != null) masterValue.text = Percent(GameSettings.MasterVolume);
            if (ambienceValue != null) ambienceValue.text = Percent(GameSettings.AmbienceVolume);
            if (musicValue != null) musicValue.text = Percent(GameSettings.MusicVolume);
            if (sfxValue != null) sfxValue.text = Percent(GameSettings.SfxVolume);
            if (vibrationValue != null) vibrationValue.text = GameSettings.Vibration
                ? L10n.Get("settings.on", "켜짐") : L10n.Get("settings.off", "꺼짐");
            if (textSpeedValue != null) textSpeedValue.text = GameSettings.TextSpeed < 33f
                ? L10n.Get("settings.slow", "느리게") : GameSettings.TextSpeed < 60f
                    ? L10n.Get("settings.normal", "보통") : L10n.Get("settings.fast", "빠르게");
            if (languageValue != null)
            {
                languageValue.text = GameSettings.Language == GameLanguage.Korean ? "한국어" : "English";
                Transform button = languageValue.transform.parent;
                if (button != null)
                {
                    button.gameObject.SetActive(GameSettings.EnglishEnabled);
                    Transform label = button.parent != null ? button.parent.Find("LanguageLabel") : null;
                    if (label != null) label.gameObject.SetActive(GameSettings.EnglishEnabled);
                }
            }
            if (screenModeValue != null) screenModeValue.text = GameSettings.Fullscreen
                ? "전체 화면" : "창 모드";
            if (resolutionValue != null) resolutionValue.text = GameSettings.ResolutionLabel;
            if (vSyncValue != null) vSyncValue.text = GameSettings.VSync
                ? L10n.Get("settings.on", "켜짐") : L10n.Get("settings.off", "꺼짐");
        }

        private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
