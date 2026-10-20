using UnityEngine;
using UnityEngine.EventSystems;

namespace ShadowTheater.UI
{
    /// <summary>Buttons feel tactile on touch, mouse, and controller without relying on scaled time.</summary>
    public sealed class HandheldButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField, Range(.85f, 1f)] private float pressedScale = .95f;
        [SerializeField, Range(1f, 1.1f)] private float focusedScale = 1.025f;
        [SerializeField, Range(1f, 40f)] private float response = 22f;

        private Vector3 _restScale;
        private bool _pressed;
        private bool _focused;

        private void Awake() => _restScale = transform.localScale;
        private void OnEnable()
        {
            _restScale = transform.localScale;
            _pressed = false;
        }

        private void OnDisable()
        {
            transform.localScale = _restScale;
            _pressed = false;
            _focused = false;
        }

        private void Update()
        {
            float factor = _pressed ? pressedScale : _focused ? focusedScale : 1f;
            Vector3 target = _restScale * factor;
            float blend = 1f - Mathf.Exp(-response * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, target, blend);
        }

        public void OnPointerDown(PointerEventData eventData) => _pressed = true;
        public void OnPointerUp(PointerEventData eventData) => _pressed = false;
        public void OnPointerExit(PointerEventData eventData) => _pressed = false;
        public void OnSelect(BaseEventData eventData) => _focused = true;
        public void OnDeselect(BaseEventData eventData) { _focused = false; _pressed = false; }
    }
}
