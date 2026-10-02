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

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (label == null) label = GetComponentInChildren<Text>();
            button?.onClick.AddListener(Select);
        }

        private void OnDestroy()
        {
            button?.onClick.RemoveListener(Select);
        }

        public void Bind(int index, string text, Action<int> onSelected)
        {
            _index = index;
            _onSelected = onSelected;
            if (label != null) label.text = text;
            gameObject.SetActive(true);
        }

        public void Clear()
        {
            _onSelected = null;
            gameObject.SetActive(false);
        }

        private void Select() => _onSelected?.Invoke(_index);
    }
}
