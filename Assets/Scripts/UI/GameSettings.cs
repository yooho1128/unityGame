using System;
using UnityEngine;

namespace ShadowTheater.UI
{
    public enum GameLanguage { Korean, English }

    /// <summary>세이브 슬롯과 독립적으로 유지되는 전역 접근성·오디오 설정.</summary>
    public class GameSettings : MonoBehaviour
    {
        public static GameSettings Instance { get; private set; }
        public static event Action Changed;

        private const string Prefix = "shadow_theater.settings.";
        private float _masterVolume;
        private float _ambienceVolume;
        private float _sfxVolume;
        private bool _vibration;
        private float _textSpeed;
        private GameLanguage _language;

        public static float MasterVolume => Instance != null ? Instance._masterVolume : 0.9f;
        public static float AmbienceVolume => Instance != null ? Instance._ambienceVolume : 0.8f;
        public static float SfxVolume => Instance != null ? Instance._sfxVolume : 0.9f;
        public static bool Vibration => Instance == null || Instance._vibration;
        public static float TextSpeed => Instance != null ? Instance._textSpeed : 42f;
        public static GameLanguage Language => Instance != null ? Instance._language : GameLanguage.Korean;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Load();
            Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetMasterVolume(float value) { _masterVolume = Mathf.Clamp01(value); ApplyAndSave(); }
        public void SetAmbienceVolume(float value) { _ambienceVolume = Mathf.Clamp01(value); ApplyAndSave(); }
        public void SetSfxVolume(float value) { _sfxVolume = Mathf.Clamp01(value); ApplyAndSave(); }
        public void SetVibration(bool value) { _vibration = value; ApplyAndSave(); }
        public void SetTextSpeed(float value) { _textSpeed = Mathf.Clamp(value, 20f, 90f); ApplyAndSave(); }
        public void SetLanguage(GameLanguage value) { _language = value; ApplyAndSave(); }

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
            _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "sfx", .9f));
            _vibration = PlayerPrefs.GetInt(Prefix + "vibration", 1) != 0;
            _textSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "text_speed", 42f), 20f, 90f);
            _language = (GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "language", 0), 0, 1);
        }

        private void ApplyAndSave()
        {
            Apply();
            PlayerPrefs.SetFloat(Prefix + "master", _masterVolume);
            PlayerPrefs.SetFloat(Prefix + "ambience", _ambienceVolume);
            PlayerPrefs.SetFloat(Prefix + "sfx", _sfxVolume);
            PlayerPrefs.SetInt(Prefix + "vibration", _vibration ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "text_speed", _textSpeed);
            PlayerPrefs.SetInt(Prefix + "language", (int)_language);
            PlayerPrefs.Save();
        }

        private void Apply()
        {
            AudioListener.volume = _masterVolume;
            Changed?.Invoke();
        }
    }
}
