using ShadowTheater.Data;
using UnityEngine;

namespace ShadowTheater.Field
{
    /// <summary>
    /// 한 장짜리 필드 픽셀 스프라이트에도 역할별 생명감을 준다.
    /// 이동 좌표는 EncounterSymbol이 담당하므로 스케일/회전만 움직여 격자 판정과 충돌하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShadowFieldMotion : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private ShadowRole role;
        [SerializeField] private GrowthTier growthTier;
        [SerializeField] private MotionStyle motionStyle;
        [SerializeField, Range(0.01f, 0.15f)] private float amount = 0.055f;
        [SerializeField, Range(1f, 12f)] private float speed = 5f;
        [SerializeField] private Sprite[] frames;

        private Vector3 _baseScale;
        private Vector3 _baseLocalPosition;
        private Quaternion _baseRotation;
        private float _phase;
        private Color _baseColor;
        private Vector3 _lastAnchorPosition;
        private float _movingUntil;
        private float _movingBlend;
        private float _nextGestureAt;
        private float _gestureUntil;

        private enum MotionStyle { Walk, Hover, Flutter, Phase }

        public void PlayGesture(float duration = .48f)
        {
            _gestureUntil = Mathf.Max(_gestureUntil, Time.time + Mathf.Max(.1f,duration));
            _nextGestureAt = Mathf.Max(_nextGestureAt, _gestureUntil + 1.2f);
        }

        public void Configure(ShadowData shadow, Sprite[] animationFrames = null)
        {
            target = GetComponent<SpriteRenderer>();
            frames = animationFrames;
            _baseScale = transform.localScale;
            _baseLocalPosition = transform.localPosition;
            _baseRotation = transform.localRotation;
            _baseColor = target != null ? target.color : Color.white;
            if (shadow == null) return;
            role = shadow.role;
            growthTier = shadow.growthTier;
            motionStyle = ResolveStyle(shadow);
            speed = motionStyle == MotionStyle.Flutter ? 7f
                : motionStyle == MotionStyle.Hover || motionStyle == MotionStyle.Phase ? 3.2f : 5.5f;
            amount = (int)growthTier >= (int)GrowthTier.RegionalBoss ? 0.075f : 0.05f;
        }

        private void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            _baseLocalPosition = transform.localPosition;
            _baseRotation = transform.localRotation;
            _baseColor = target != null ? target.color : Color.white;
            _lastAnchorPosition = transform.parent != null ? transform.parent.position : transform.position;
            _phase = (GetInstanceID() & 31) * 0.37f;
            _nextGestureAt = Time.time + 2.2f + (GetInstanceID() & 7) * .43f;
        }

        private void LateUpdate()
        {
            if (target == null || !target.enabled) return;
            float wave = Mathf.Sin((Time.time + _phase) * speed);
            float lift = Mathf.Sin((Time.time + _phase) * speed * 0.5f);
            Vector3 anchorPosition = transform.parent != null ? transform.parent.position : transform.position;
            if ((anchorPosition - _lastAnchorPosition).sqrMagnitude > .000001f) _movingUntil = Time.time + .12f;
            _lastAnchorPosition = anchorPosition;
            bool moving = Time.time <= _movingUntil;
            _movingBlend = Mathf.MoveTowards(_movingBlend, moving ? 1f : 0f, Time.deltaTime * (moving ? 9f : 5f));
            if (moving) _nextGestureAt = Mathf.Max(_nextGestureAt, Time.time + 1.2f);
            else if (Time.time >= _nextGestureAt)
            {
                _gestureUntil = Time.time + .42f;
                _nextGestureAt = Time.time + 3.1f + (GetInstanceID() & 3) * .71f;
            }
            float gesture = Time.time < _gestureUntil
                ? Mathf.Sin(Mathf.InverseLerp(_gestureUntil - .42f, _gestureUntil, Time.time) * Mathf.PI) : 0f;
            if (frames != null && frames.Length > 0)
            {
                int frame = motionStyle == MotionStyle.Walk && !moving
                    ? 0 : Mathf.FloorToInt((Time.time + _phase) * speed) % frames.Length;
                target.sprite = frames[Mathf.Abs(frame)];
            }

            switch (motionStyle)
            {
                case MotionStyle.Hover:
                    // 공중형도 대기 중에는 낮게 숨 쉬고, 이동할 때만 부유 폭을 키운다.
                    float hoverAmount = amount * Mathf.Lerp(.28f, 1f, _movingBlend);
                    transform.localScale = Vector3.Scale(_baseScale,
                        new Vector3(1f - lift * hoverAmount * .22f, 1f + lift * hoverAmount, 1f));
                    transform.localPosition = _baseLocalPosition + Vector3.up * (lift * hoverAmount * .18f);
                    transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, wave * .8f + gesture * 1.8f);
                    break;
                case MotionStyle.Flutter:
                    float flutterAmount = amount * Mathf.Lerp(.35f, 1f, _movingBlend);
                    transform.localScale = Vector3.Scale(_baseScale,
                        new Vector3(1f + Mathf.Abs(wave) * flutterAmount, 1f - Mathf.Abs(wave) * flutterAmount * .45f, 1f));
                    transform.localPosition = _baseLocalPosition + Vector3.up * (lift * flutterAmount * .12f);
                    transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, wave * Mathf.Lerp(1.2f,3.2f,_movingBlend));
                    break;
                case MotionStyle.Phase:
                    // 각본 밖 존재: 폭이 사라졌다 돌아오며 페이지처럼 위상이 흔들린다.
                    transform.localScale = Vector3.Scale(_baseScale,
                        new Vector3(1f + wave * amount * 1.2f, 1f - wave * amount * .35f, 1f));
                    transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, lift * 2.2f);
                    transform.localPosition = _baseLocalPosition;
                    target.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, .88f + .12f * Mathf.Abs(lift));
                    break;
                default:
                    // 보행형은 시각 몸체만 들어 올려 접지 그림자와 분리하고, 실제 충돌 좌표는 유지한다.
                    float step = Mathf.Abs(wave) * _movingBlend;
                    transform.localScale = Vector3.Scale(_baseScale,
                        new Vector3(1f + step * amount * .45f, 1f - step * amount + gesture * amount * .18f, 1f));
                    transform.localPosition = _baseLocalPosition + Vector3.up * (step * amount * .42f);
                    transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f,
                        wave * 1.15f * _movingBlend + gesture * 2.2f);
                    break;
            }
        }

        private static MotionStyle ResolveStyle(ShadowData shadow)
        {
            switch (shadow.shadowId)
            {
                case "inverted_guide": return MotionStyle.Walk;
                case "silent_applause": return MotionStyle.Flutter;
                case "outside_script_shadow": return MotionStyle.Phase;
            }
            if (shadow.role == ShadowRole.SpeedUtility) return MotionStyle.Flutter;
            if (shadow.role == ShadowRole.MagicNuker || shadow.role == ShadowRole.Support) return MotionStyle.Hover;
            return MotionStyle.Walk;
        }

        private void OnDisable()
        {
            transform.localScale = _baseScale;
            transform.localPosition = _baseLocalPosition;
            transform.localRotation = _baseRotation;
            if (target != null) target.color = _baseColor;
        }
    }
}
