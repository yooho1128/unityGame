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
        private LayoutElement _layout;
        private float _lastWidth = -1f;

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
            _lastWidth = -1f;
        }

        private void LateUpdate()
        {
            if (subtitleText != null && Mathf.Abs(subtitleText.rectTransform.rect.width - _lastWidth) > .5f)
                FitContent();
        }

        /// <summary>설명 줄 수에 맞춰 행을 늘려, 긴 기술 정보와 사용 불가 이유를 모두 읽을 수 있게 한다.</summary>
        public void FitContent()
        {
            if (titleText == null || subtitleText == null) return;
            if (_layout == null) _layout = GetComponent<LayoutElement>();
            if (_layout == null) return;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            float titleHeight = Mathf.Max(30f, titleText.preferredHeight);
            float subtitleHeight = Mathf.Max(20f, subtitleText.preferredHeight);
            _layout.preferredHeight = Mathf.Max(88f, titleHeight + subtitleHeight + 24f);
            var title = titleText.rectTransform;
            title.anchorMin = new Vector2(.03f, 1f); title.anchorMax = new Vector2(.97f, 1f);
            title.offsetMin = new Vector2(0f, -8f - titleHeight); title.offsetMax = new Vector2(0f, -8f);
            var subtitle = subtitleText.rectTransform;
            subtitle.anchorMin = new Vector2(.03f, 1f); subtitle.anchorMax = new Vector2(.97f, 1f);
            subtitle.offsetMin = new Vector2(0f, -16f - titleHeight - subtitleHeight);
            subtitle.offsetMax = new Vector2(0f, -16f - titleHeight);
            _lastWidth = subtitle.rect.width;
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
