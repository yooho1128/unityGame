using ShadowTheater.UI;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>가까이 온 플레이어를 바라보고, 처음 발견했을 때 작은 몸짓을 보이는 NPC 생활감 레이어.</summary>
    [DisallowMultipleComponent]
    public sealed class FieldNpcPresence : MonoBehaviour
    {
        [SerializeField, Range(1f, 6f)] private float noticeRadius = 3.1f;
        [SerializeField, Range(.5f, 3f)] private float relaxRadiusBonus = 1.1f;

        private SpriteRenderer _renderer;
        private Transform _visual;
        private Vector3 _baseScale;
        private ShadowFieldMotion _motion;
        private bool _noticed;
        private float _noticeAt;
        private float _nextLookAt;
        private SpriteRenderer _emote;

        private void Awake()
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
            _visual = _renderer != null ? _renderer.transform : transform;
            _baseScale = _visual.localScale;
            _motion = GetComponentInChildren<ShadowFieldMotion>();
            var icon = new GameObject("NoticeEmote", typeof(SpriteRenderer));
            icon.transform.SetParent(transform, false); icon.transform.localPosition = new Vector3(0f,1.05f,0f);
            _emote = icon.GetComponent<SpriteRenderer>(); _emote.sprite = BattleFxSpriteLibrary.Get(BattleFxGlyph.Star);
            _emote.color = new Color(1f,.86f,.3f,0f); _emote.sortingOrder = 18;
        }

        private void Update()
        {
            PlayerController player = PlayerController.Instance;
            if (player == null || _renderer == null) return;
            float distance = Vector2.Distance(transform.position, player.transform.position);
            bool near = distance <= noticeRadius && !player.IsMoving;
            if (near && !_noticed)
            {
                _noticed = true; _noticeAt = Time.time; _nextLookAt = Time.time;
                _motion?.PlayGesture(.55f);
            }
            else if (_noticed && distance > noticeRadius + relaxRadiusBonus) _noticed = false;

            if (_noticed && Time.time >= _nextLookAt)
            {
                float dx = player.transform.position.x - transform.position.x;
                if (Mathf.Abs(dx) > .08f) _renderer.flipX = dx < 0f;
                _nextLookAt = Time.time + .2f;
            }

            float greeting = _noticed ? Mathf.Clamp01(1f - (Time.time - _noticeAt) / .55f) : 0f;
            float bounce = greeting * Mathf.Sin(Mathf.Clamp01((Time.time-_noticeAt)/.55f) * Mathf.PI);
            if (_emote != null)
            {
                Color c = _emote.color; c.a = greeting * .9f; _emote.color = c;
                _emote.transform.localScale = Vector3.one * (.12f + bounce * .13f);
            }
        }

        private void OnDisable()
        {
            if (_visual != null && _motion == null) _visual.localScale = _baseScale;
        }
    }
}
