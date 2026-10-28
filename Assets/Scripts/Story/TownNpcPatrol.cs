using UnityEngine;
using ShadowTheater.Field;

namespace ShadowTheater.Story
{
    /// <summary>미끄러지는 왕복 대신 목적지에서 쉬고, 가속해 걷고, 천천히 멈추는 생활 NPC 이동.</summary>
    public class TownNpcPatrol : MonoBehaviour
    {
        [SerializeField, Range(0,3)] private int requiredStage = 2;
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
        private bool _watchingPlayer;
        private ShadowFieldMotion _motion;
        private bool _sheltering;
        private bool _nightRest;
        private Vector3 _scheduleAnchor;
        private Vector3 _schedulePatrolOffset;
        private Vector3 _motionPosition;
        private FieldTimeOfDay _timeOfDay;
        private const float PixelsPerUnit = 32f;

        private void Awake()
        {
            _origin = transform.localPosition;
            _enabled = requiredStage <= 0;
            _scheduleAnchor = _origin;
            _schedulePatrolOffset = localOffset;
            _target = _origin;
            _motionPosition = _origin;
            _renderer = GetComponentInChildren<SpriteRenderer>();
            _motion = GetComponentInChildren<ShadowFieldMotion>();
            _waitUntil = Time.time + StableDelay(.4f, 1.4f);
        }

        public void SetRestorationStage(int stage)
        {
            _enabled = stage >= requiredStage;
            if (_enabled && _waitUntil < Time.time) _waitUntil = Time.time + StableDelay(.3f, 1.1f);
        }

        public void SetWorldState(FieldWorldSnapshot state)
        {
            bool scheduleChanged=_timeOfDay!=state.timeOfDay;
            _timeOfDay=state.timeOfDay;
            _nightRest=state.timeOfDay==FieldTimeOfDay.Night;
            _sheltering=state.weather==FieldWeather.Rain||state.weather==FieldWeather.Snow||
                state.weather==FieldWeather.InkStorm;
            float routePosition=state.timeOfDay==FieldTimeOfDay.Day?.55f:
                state.timeOfDay==FieldTimeOfDay.Evening?.22f:0f;
            _scheduleAnchor=_origin+(Vector3)(localOffset*routePosition);
            _schedulePatrolOffset=localOffset*(state.timeOfDay==FieldTimeOfDay.Dawn?.22f:.34f);
            if(_nightRest||_sheltering){_scheduleAnchor=_origin;_schedulePatrolOffset=Vector3.zero;}
            if(scheduleChanged||_nightRest||_sheltering)
            {
                _target=_scheduleAnchor;_atFarEnd=false;
                _waitUntil=Mathf.Min(_waitUntil,Time.time+.35f);
            }
        }

        private void Update()
        {
            if (!_enabled) return;
            PlayerController player = PlayerController.Instance;
            if (player != null && Vector2.Distance(transform.position,player.transform.position) < 2.25f)
            {
                _velocity = Vector3.zero;
                float playerDx = player.transform.position.x-transform.position.x;
                if (_renderer != null && Mathf.Abs(playerDx)>.08f) _renderer.flipX=playerDx<0f;
                if (!_watchingPlayer) _motion?.PlayGesture(.52f);
                _watchingPlayer = true;
                _waitUntil = Time.time + .36f;
                return;
            }
            if (_watchingPlayer)
            {
                _watchingPlayer = false;
                _waitUntil = Mathf.Max(_waitUntil,Time.time+.42f);
            }
            if((_nightRest||_sheltering)&&(_scheduleAnchor-_motionPosition).sqrMagnitude<.01f)
            {_motionPosition=_scheduleAnchor;transform.localPosition=PixelSnap(_motionPosition);_velocity=Vector3.zero;return;}
            if (Time.time < _waitUntil) return;
            if ((_target - _motionPosition).sqrMagnitude < .0025f)
            {
                _motionPosition = _target;
                transform.localPosition = PixelSnap(_motionPosition);
                _velocity = Vector3.zero;
                _atFarEnd = !_atFarEnd;
                _target = _atFarEnd ? _scheduleAnchor + _schedulePatrolOffset : _scheduleAnchor;
                _waitUntil = Time.time + StableDelay(pauseRange.x, pauseRange.y);
                if (StableDelay(0f,1f)>.58f) _motion?.PlayGesture(.38f);
                return;
            }

            float dx = _target.x - _motionPosition.x;
            if (_renderer != null && Mathf.Abs(dx) > .01f) _renderer.flipX = dx < 0f;
            _motionPosition = Vector3.SmoothDamp(_motionPosition, _target,
                ref _velocity, .16f, speed, Time.deltaTime);
            transform.localPosition = PixelSnap(_motionPosition);
        }

        private static Vector3 PixelSnap(Vector3 position) => new Vector3(
            Mathf.Round(position.x*PixelsPerUnit)/PixelsPerUnit,
            Mathf.Round(position.y*PixelsPerUnit)/PixelsPerUnit,
            position.z);

        private float StableDelay(float min, float max)
        {
            float seed = Mathf.Abs(Mathf.Sin((GetInstanceID() & 1023) * 12.9898f + (_atFarEnd ? 4.1f : .7f)));
            return Mathf.Lerp(min, max, seed);
        }
    }
}
