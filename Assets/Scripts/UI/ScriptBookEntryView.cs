using System;
using ShadowTheater.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public enum ScriptBookEntryState { Unknown, Seen, Recorded }

    public class ScriptBookEntryView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image silhouette;
        [SerializeField] private Image accent;
        [SerializeField] private Text numberText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text stateText;

        private ShadowData _data;
        private Action<ShadowData> _onSelected;
        private bool _bound;
        private ScriptBookEntryState _state;

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            EnsureBinding();
        }

        private void OnDestroy() => button?.onClick.RemoveListener(Select);

        public void Bind(ShadowData data, int index, ScriptBookEntryState state, Action<ShadowData> onSelected)
        {
            EnsureBinding();
            _data = data;
            _state = state;
            _onSelected = onSelected;
            if (numberText != null) numberText.text = $"No.{index + 1:000}";
            if (nameText != null) nameText.text = state == ScriptBookEntryState.Unknown
                ? "???" : L10n.Text(data.displayName);
            if (stateText != null) stateText.text = StateLabel(state);
            if (silhouette != null)
            {
                silhouette.sprite = state == ScriptBookEntryState.Unknown ? null : data.silhouetteSprite;
                silhouette.enabled = silhouette.sprite != null;
                silhouette.color = state == ScriptBookEntryState.Recorded
                    ? ShadowPortraitStyle.Tint(silhouette.sprite)
                    : Color.black;
                silhouette.preserveAspect = true;
            }
            if (accent != null)
                accent.color = state == ScriptBookEntryState.Recorded
                    ? data.accentColor
                    : new Color(0.20f, 0.18f, 0.27f, 1f);
            gameObject.SetActive(true);
        }

        public void SetSelected(string shadowId)
        {
            if (stateText != null) stateText.text = (_data?.shadowId == shadowId ? "▶ " : "") + StateLabel(_state);
        }

        private void Select() => _onSelected?.Invoke(_data);
        private void EnsureBinding()
        {
            if (button == null) button = GetComponent<Button>();
            if (_bound || button == null) return;
            button.onClick.AddListener(Select); _bound = true;
        }

        private static string StateLabel(ScriptBookEntryState state)
        {
            switch (state)
            {
                case ScriptBookEntryState.Recorded: return L10n.Get("common.complete", "기록 완료");
                case ScriptBookEntryState.Seen: return L10n.Get("common.encountered", "조우");
                default: return L10n.Get("common.not_seen", "미조우");
            }
        }
    }
}
