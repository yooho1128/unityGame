using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    /// <summary>Buttons feel tactile on touch, mouse, and controller without relying on scaled time.</summary>
    [DisallowMultipleComponent]
    public sealed class HandheldButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField, Range(.85f, 1f)] private float pressedScale = .98f;
        [SerializeField, Range(1f, 1.1f)] private float focusedScale = 1.01f;
        [SerializeField, Range(1f, 40f)] private float response = 22f;

        private Vector3 _restScale;
        private bool _pressed;
        private bool _focused;
        private bool _hovered;
        private Button _button;
        private Image _focusEdge;
        private float _submitUntil;

        private void Awake()
        {
            _restScale = transform.localScale;
            _button = GetComponent<Button>();
        }

        private void Start()
        {
            // A narrow edge stays visible on both dark and gold button surfaces.
            var rect = transform.Find("FocusEdge") as RectTransform;
            if (rect == null)
                rect = RuntimeUiControls.Rect(transform, "FocusEdge", new Vector2(0f, .16f), new Vector2(0f, .84f));
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(4f, 0f);
            rect.sizeDelta = new Vector2(3f, 0f);
            var layout = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            _focusEdge = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            _focusEdge.raycastTarget = false;
            _focusEdge.color = new Color(TheatreUiStyle.Ivory.r, TheatreUiStyle.Ivory.g, TheatreUiStyle.Ivory.b, 0f);
        }
        private void OnEnable()
        {
            _restScale = transform.localScale;
            _pressed = false;
            _focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        }

        private void OnDisable()
        {
            transform.localScale = _restScale;
            _pressed = false;
            _focused = false;
            _hovered = false;
            _submitUntil = 0f;
            if (_focusEdge != null)
            {
                var color = _focusEdge.color; color.a = 0f; _focusEdge.color = color;
            }
        }

        private void Update()
        {
            bool available = _button != null && _button.IsActive() && _button.IsInteractable();
            if (!available) { _pressed = false; _submitUntil = 0f; }
            bool pressed = available && (_pressed || Time.unscaledTime < _submitUntil);
            bool focused = available && (_focused || _hovered);
            float factor = pressed ? Mathf.Max(.98f, pressedScale) : focused ? Mathf.Min(1.01f, focusedScale) : 1f;
            if (GameSettings.ReducedMotion) factor = 1f;
            Vector3 target = _restScale * factor;
            float blend = 1f - Mathf.Exp(-response * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, target, blend);
            if (_focusEdge != null)
            {
                var color = _focusEdge.color;
                color.a = Mathf.Lerp(color.a, focused || pressed ? 1f : 0f, blend);
                _focusEdge.color = color;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && _button != null && _button.IsInteractable())
                _pressed = true;
        }
        public void OnPointerUp(PointerEventData eventData)
        { if (eventData.button == PointerEventData.InputButton.Left) _pressed = false; }
        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
        public void OnPointerExit(PointerEventData eventData) { _pressed = false; _hovered = false; }
        public void OnSelect(BaseEventData eventData) => _focused = true;
        public void OnDeselect(BaseEventData eventData) { _focused = false; _pressed = false; }
        public void OnSubmit(BaseEventData eventData)
        {
            if (_button != null && _button.IsInteractable()) _submitUntil = Time.unscaledTime + .10f;
        }
    }
}
