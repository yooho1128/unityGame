using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class DialogueChoiceView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;

        private int _index;
        private Action<int> _onSelected;
        private bool _bound;

        private void Awake()
        {
            EnsureBinding();
        }

        private void EnsureBinding()
        {
            if (button == null) button = GetComponent<Button>();
            if (label == null) label = GetComponentInChildren<Text>();
            if (button == null || _bound) return;
            button.onClick.AddListener(Select);
            _bound = true;
        }

        private void OnDestroy()
        {
            button?.onClick.RemoveListener(Select);
        }

        public void Bind(int index, string text, Action<int> onSelected)
        {
            EnsureBinding();
            _index = index;
            _onSelected = onSelected;
            if (label != null) label.text = text;
            if (button != null) button.interactable = true;
            gameObject.SetActive(true);
        }

        public void Clear()
        {
            _onSelected = null;
            if (button != null) button.interactable = false;
            gameObject.SetActive(false);
        }

        private void Select()
        {
            if (button != null && button.interactable) _onSelected?.Invoke(_index);
        }
    }
}
