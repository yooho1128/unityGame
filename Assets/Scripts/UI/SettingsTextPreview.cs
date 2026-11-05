using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>A silent, repeatable sample with the same speed and punctuation pauses as story dialogue.</summary>
    public sealed class SettingsTextPreview : MonoBehaviour
    {
        private Text _label;
        private string _sample;
        private float _characters, _pause, _speed;
        private int _shown;

        public void Initialize(Text label) { _label = label; Replay(); }
        private void OnEnable()
        {
            GameSettings.Changed += OnSettingsChanged;
            L10n.Changed += Replay;
            Replay();
        }
        private void OnDisable()
        {
            GameSettings.Changed -= OnSettingsChanged;
            L10n.Changed -= Replay;
        }
        private void OnSettingsChanged()
        { if (!Mathf.Approximately(_speed, GameSettings.TextSpeed)) Replay(); }

        public void Replay()
        {
            if (_label == null) return;
            _speed = GameSettings.TextSpeed;
            _sample = L10n.Get("settings.preview_line", "막이 오르면, 우리의 이야기가 다시 시작됩니다.");
            _characters = _pause = 0f; _shown = 0;
            _label.text = string.Empty;
        }
        private void Update()
        {
            if (_label == null || string.IsNullOrEmpty(_sample) || _shown >= _sample.Length) return;
            if (_pause > 0f) { _pause -= Time.unscaledDeltaTime; return; }
            _characters += _speed * Time.unscaledDeltaTime;
            int count = Mathf.Min(_sample.Length, Mathf.FloorToInt(_characters));
            for (int i = _shown; i < count; i++)
            {
                _pause = DialogueTextPacing.PauseAfter(_sample[i], i == _sample.Length - 1);
                if (_pause > 0f) { count = i + 1; _characters = count; break; }
            }
            if (count == _shown) return;
            _shown = count; _label.text = _sample.Substring(0, count);
        }
    }
}
