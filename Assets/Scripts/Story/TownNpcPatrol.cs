using UnityEngine;
using ShadowTheater.Field;

namespace ShadowTheater.Story
{
    /// <summary>미끄러지는 왕복 대신 목적지에서 쉬고, 가속해 걷고, 천천히 멈추는 생활 NPC 이동.</summary>
    public class TownNpcPatrol : MonoBehaviour
    {
        [SerializeField, Range(1,3)] private int requiredStage = 2;
        [SerializeField] private Vector2 localOffset = new Vector2(2f,0);
        [SerializeField, Min(.1f)] private float speed = .65f;
        [SerializeField] private Vector2 pauseRange = new Vector2(1.1f, 2.8f);

        private Vector3 _origin;
        private Vector3 _target;
        private Vector3 _velocity;
        private SpriteRenderer _renderer;
        private bool _enabled;
        private bool _atFarEnd;
        private float _waitUntil;

        private void Awake()
        {
            _origin = transform.localPosition;
            _target = _origin;
            _renderer = GetComponentInChildren<SpriteRenderer>();
            _waitUntil = Time.time + StableDelay(.4f, 1.4f);
        }

        public void SetRestorationStage(int stage)
        {
            _enabled = stage >= requiredStage;
            if (_enabled && _waitUntil < Time.time) _waitUntil = Time.time + StableDelay(.3f, 1.1f);
        }

        private void Update()
        {
            if (!_enabled || Time.time < _waitUntil) return;
            PlayerController player = PlayerController.Instance;
            if (player != null && Vector2.Distance(transform.position,player.transform.position) < 2.25f)
            {
                _velocity = Vector3.zero;
                _waitUntil = Time.time + .18f;
                return;
            }
            if ((_target - transform.localPosition).sqrMagnitude < .0025f)
            {
                transform.localPosition = _target;
                _velocity = Vector3.zero;
                _atFarEnd = !_atFarEnd;
                _target = _atFarEnd ? _origin + (Vector3)localOffset : _origin;
                _waitUntil = Time.time + StableDelay(pauseRange.x, pauseRange.y);
                return;
            }

            float dx = _target.x - transform.localPosition.x;
            if (_renderer != null && Mathf.Abs(dx) > .01f) _renderer.flipX = dx < 0f;
            transform.localPosition = Vector3.SmoothDamp(transform.localPosition, _target,
                ref _velocity, .16f, speed, Time.deltaTime);
        }

        private float StableDelay(float min, float max)
        {
            float seed = Mathf.Abs(Mathf.Sin((GetInstanceID() & 1023) * 12.9898f + (_atFarEnd ? 4.1f : .7f)));
            return Mathf.Lerp(min, max, seed);
        }
    }
}
