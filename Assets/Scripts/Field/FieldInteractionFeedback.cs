using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>바라보는 상호작용 대상 위에 문맥 표식을 띄우고 대상 자체도 가볍게 강조한다.</summary>
    [DisallowMultipleComponent]
    public sealed class FieldInteractionFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        private SpriteRenderer _prompt;
        private Transform _target;
        private SpriteRenderer _targetRenderer;
        private Color _targetColor;
        private float _confirmUntil;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            var icon = new GameObject("InteractionPrompt", typeof(SpriteRenderer));
            icon.transform.SetParent(transform.parent, false);
            _prompt = icon.GetComponent<SpriteRenderer>();
            _prompt.sprite = BattleFxSpriteLibrary.Get(BattleFxGlyph.Rune);
            _prompt.color = new Color(1f,.88f,.35f,0f); _prompt.sortingOrder = 90;
        }

        public void Configure(PlayerController owner) => controller = owner;

        public void PlayConfirm()
        {
            _confirmUntil = Time.unscaledTime + .28f;
            if (_prompt != null) _prompt.sprite = BattleFxSpriteLibrary.Get(BattleFxGlyph.Star);
            FieldCameraFollow.Instance?.Nudge(Vector2.up*.025f);
        }

        private void LateUpdate()
        {
            Component component = null;
            if (controller != null && !controller.IsLocked && !controller.IsMoving &&
                controller.TryGetInteraction(out IInteractable interaction)) component = interaction as Component;
            Transform next = component != null ? component.transform : null;
            if (Time.unscaledTime >= _confirmUntil && next != _target) SetTarget(next);
            if (_prompt == null) return;
            float visible = _target != null ? 1f : 0f;
            Color color = _prompt.color; color.a = Mathf.MoveTowards(color.a, visible, Time.unscaledDeltaTime * 8f); _prompt.color = color;
            if (_target != null)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * .1f;
                if (Time.unscaledTime < _confirmUntil) pulse += .55f * Mathf.Sin(
                    Mathf.InverseLerp(_confirmUntil-.28f,_confirmUntil,Time.unscaledTime)*Mathf.PI);
                else if (_prompt.sprite != BattleFxSpriteLibrary.Get(BattleFxGlyph.Rune))
                    _prompt.sprite = BattleFxSpriteLibrary.Get(BattleFxGlyph.Rune);
                _prompt.transform.position = _target.position + Vector3.up * (1.03f + Mathf.Sin(Time.unscaledTime*4f)*.035f);
                _prompt.transform.localScale = Vector3.one * (.19f * pulse);
                if (_targetRenderer != null) _targetRenderer.color = Color.Lerp(_targetColor, Color.white, .14f + Mathf.Sin(Time.time*5f)*.04f);
            }
        }

        private void SetTarget(Transform next)
        {
            RestoreTarget(); _target = next;
            if (_target == null) return;
            _targetRenderer = _target.GetComponentInChildren<SpriteRenderer>();
            if (_targetRenderer != null) _targetColor = _targetRenderer.color;
        }

        private void RestoreTarget()
        {
            if (_targetRenderer != null) _targetRenderer.color = _targetColor;
            _targetRenderer = null;
        }

        private void OnDisable()
        {
            RestoreTarget();
            if (_prompt != null) { Color c=_prompt.color;c.a=0f;_prompt.color=c; }
        }

        private void OnDestroy() { if (_prompt != null) Destroy(_prompt.gameObject); }
    }
}
