using System;
using UnityEngine;

namespace ShadowTheater.UI
{
    public enum GameLanguage { Korean, English }

    /// <summary>세이브 슬롯과 독립적으로 유지되는 전역 접근성·오디오 설정.</summary>
    public class GameSettings : MonoBehaviour
    {
        public const bool EnglishEnabled = false;
        public static GameSettings Instance { get; private set; }
        public static event Action Changed;

        private const string Prefix = "shadow_theater.settings.";
        private float _masterVolume;
        private float _ambienceVolume;
        private float _musicVolume;
        private float _sfxVolume;
        private bool _vibration;
        private float _textSpeed;
        private GameLanguage _language;
        private bool _fullscreen;
        private bool _vSync;
        private int _resolutionIndex;

        private static readonly Vector2Int[] DesktopResolutions =
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080), new Vector2Int(2560, 1440)
        };

        public static float MasterVolume => Instance != null ? Instance._masterVolume : 0.9f;
        public static float AmbienceVolume => Instance != null ? Instance._ambienceVolume : 0.8f;
        public static float MusicVolume => Instance != null ? Instance._musicVolume : 0.75f;
        public static float SfxVolume => Instance != null ? Instance._sfxVolume : 0.9f;
        public static bool Vibration => Instance == null || Instance._vibration;
        public static float TextSpeed => Instance != null ? Instance._textSpeed : 42f;
        public static GameLanguage Language => Instance != null ? Instance._language : GameLanguage.Korean;
        public static bool Fullscreen => Instance == null || Instance._fullscreen;
        public static bool VSync => Instance == null || Instance._vSync;
        public static string ResolutionLabel
        {
            get
            {
                int index = Instance != null ? Instance._resolutionIndex : 2;
                Vector2Int size = DesktopResolutions[Mathf.Clamp(index, 0, DesktopResolutions.Length - 1)];
                return $"{size.x} × {size.y}";
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Load();
            Apply();
            ApplyDisplay();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetMasterVolume(float value) { _masterVolume = Mathf.Clamp01(value); ApplyAndSave(); }
        public void SetAmbienceVolume(float value) { _ambienceVolume = Mathf.Clamp01(value); ApplyAndSave(); }
        public void SetMusicVolume(float value) { _musicVolume = Mathf.Clamp01(value); ApplyAndSave(); }
        public void SetSfxVolume(float value) { _sfxVolume = Mathf.Clamp01(value); ApplyAndSave(); }
        public void SetVibration(bool value) { _vibration = value; ApplyAndSave(); }
        public void SetTextSpeed(float value) { _textSpeed = Mathf.Clamp(value, 20f, 90f); ApplyAndSave(); }
        public void SetLanguage(GameLanguage value)
        {
            _language = EnglishEnabled ? value : GameLanguage.Korean;
            ApplyAndSave();
        }

        public void ToggleFullscreen() { _fullscreen = !_fullscreen; ApplyDisplayAndSave(); }
        public void ToggleVSync() { _vSync = !_vSync; ApplyDisplayAndSave(); }
        public void CycleResolution()
        {
            _resolutionIndex = (_resolutionIndex + 1) % DesktopResolutions.Length;
            ApplyDisplayAndSave();
        }

        public static void TryVibrate()
        {
            if (!Vibration) return;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        private void Load()
        {
            _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "master", .9f));
            _ambienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "ambience", .8f));
            _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "music", .75f));
            _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "sfx", .9f));
            _vibration = PlayerPrefs.GetInt(Prefix + "vibration", 1) != 0;
            _textSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "text_speed", 42f), 20f, 90f);
            _language = EnglishEnabled
                ? (GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "language", 0), 0, 1)
                : GameLanguage.Korean;
            _fullscreen = PlayerPrefs.GetInt(Prefix + "fullscreen", 1) != 0;
            _vSync = PlayerPrefs.GetInt(Prefix + "vsync", 1) != 0;
            _resolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "resolution", 2), 0,
                DesktopResolutions.Length - 1);
        }

        private void ApplyAndSave()
        {
            Apply();
            SavePreferences();
        }

        private void ApplyDisplayAndSave()
        {
            ApplyDisplay();
            Changed?.Invoke();
            SavePreferences();
        }

        private void SavePreferences()
        {
            PlayerPrefs.SetFloat(Prefix + "master", _masterVolume);
            PlayerPrefs.SetFloat(Prefix + "ambience", _ambienceVolume);
            PlayerPrefs.SetFloat(Prefix + "music", _musicVolume);
            PlayerPrefs.SetFloat(Prefix + "sfx", _sfxVolume);
            PlayerPrefs.SetInt(Prefix + "vibration", _vibration ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "text_speed", _textSpeed);
            PlayerPrefs.SetInt(Prefix + "language", (int)_language);
            PlayerPrefs.SetInt(Prefix + "fullscreen", _fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "vsync", _vSync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "resolution", _resolutionIndex);
            PlayerPrefs.Save();
        }

        private void Apply()
        {
            AudioListener.volume = _masterVolume;
            Changed?.Invoke();
        }

        private void ApplyDisplay()
        {
#if UNITY_STANDALONE
            QualitySettings.vSyncCount = _vSync ? 1 : 0;
            Application.targetFrameRate = _vSync ? -1 : 120;
            Vector2Int size = DesktopResolutions[_resolutionIndex];
            Screen.SetResolution(size.x, size.y,
                _fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        }
    }
}
