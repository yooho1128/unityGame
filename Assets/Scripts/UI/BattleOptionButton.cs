using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class BattleOptionButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        private Action _action;
        private bool _bound;

        private void Awake()
        {
            EnsureBinding();
        }

        private void OnDestroy() => button?.onClick.RemoveListener(Select);

        public void Bind(string title, string subtitle, bool interactable, Action action)
        {
            EnsureBinding();
            _action = action;
            if (titleText != null) titleText.text = title;
            if (subtitleText != null) subtitleText.text = subtitle;
            if (button != null) button.interactable = interactable;
            gameObject.SetActive(true);
        }
        private void EnsureBinding()
        {
            if (button == null) button = GetComponent<Button>();
            if (_bound || button == null) return;
            button.onClick.AddListener(Select); _bound = true;
        }

        private void Select()
        {
            if (button != null && button.interactable) _action?.Invoke();
        }
    }
}
