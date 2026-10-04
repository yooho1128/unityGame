using UnityEngine;
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
        private bool _refreshing;

        private void Awake() { if (root != null) root.SetActive(false); }

        public void Open()
        {
            if (titleRoot != null) titleRoot.SetActive(false);
            if (root != null) root.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
            if (titleRoot != null) titleRoot.SetActive(true);
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
            GameSettings.Instance.SetLanguage(GameSettings.Language == GameLanguage.Korean
                ? GameLanguage.English : GameLanguage.Korean);
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
            if (languageValue != null) languageValue.text = GameSettings.Language == GameLanguage.Korean ? "한국어" : "English";
        }

        private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
