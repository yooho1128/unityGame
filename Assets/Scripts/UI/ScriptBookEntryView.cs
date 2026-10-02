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

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            button?.onClick.AddListener(Select);
        }

        private void OnDestroy() => button?.onClick.RemoveListener(Select);

        public void Bind(ShadowData data, int index, ScriptBookEntryState state, Action<ShadowData> onSelected)
        {
            _data = data;
            _onSelected = onSelected;
            if (numberText != null) numberText.text = $"No.{index + 1:000}";
            if (nameText != null) nameText.text = state == ScriptBookEntryState.Unknown ? "???" : data.displayName;
            if (stateText != null) stateText.text = StateLabel(state);
            if (silhouette != null)
            {
                silhouette.sprite = state == ScriptBookEntryState.Unknown ? null : data.silhouetteSprite;
                silhouette.color = state == ScriptBookEntryState.Recorded
                    ? new Color(0.08f, 0.06f, 0.12f, 1f)
                    : Color.black;
                silhouette.preserveAspect = true;
            }
            if (accent != null)
                accent.color = state == ScriptBookEntryState.Recorded
                    ? data.accentColor
                    : new Color(0.20f, 0.18f, 0.27f, 1f);
            gameObject.SetActive(true);
        }

        private void Select() => _onSelected?.Invoke(_data);

        private static string StateLabel(ScriptBookEntryState state)
        {
            switch (state)
            {
                case ScriptBookEntryState.Recorded: return "기록 완료";
                case ScriptBookEntryState.Seen: return "조우";
                default: return "미조우";
            }
        }
    }
}
