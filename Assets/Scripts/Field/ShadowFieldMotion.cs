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
        private Quaternion _baseRotation;
        private float _phase;
        private Color _baseColor;
        private Vector3 _lastAnchorPosition;
        private float _movingUntil;

        private enum MotionStyle { Walk, Hover, Flutter, Phase }

        public void Configure(ShadowData shadow, Sprite[] animationFrames = null)
        {
            target = GetComponent<SpriteRenderer>();
            frames = animationFrames;
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
            _baseRotation = transform.localRotation;
            _baseColor = target != null ? target.color : Color.white;
            _lastAnchorPosition = transform.parent != null ? transform.parent.position : transform.position;
            _phase = (GetInstanceID() & 31) * 0.37f;
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
            if (frames != null && frames.Length > 0)
            {
                int frame = motionStyle == MotionStyle.Walk && !moving
                    ? 0 : Mathf.FloorToInt((Time.time + _phase) * speed) % frames.Length;
                target.sprite = frames[Mathf.Abs(frame)];
            }

            switch (motionStyle)
            {
                case MotionStyle.Hover:
                    // 공중 부유: 천천히 늘어나고 좌우로 떠돈다.
                    transform.localScale = Vector3.Scale(_baseScale,
                        new Vector3(1f - lift * amount * .3f, 1f + lift * amount, 1f));
                    transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, wave * 1.8f);
                    break;
                case MotionStyle.Flutter:
                    // 날갯짓/활공: 빠른 좌우 기울기와 짧은 수축.
                    transform.localScale = Vector3.Scale(_baseScale,
                        new Vector3(1f + Mathf.Abs(wave) * amount, 1f - Mathf.Abs(wave) * amount * .45f, 1f));
                    transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, wave * 3.2f);
                    break;
                case MotionStyle.Phase:
                    // 각본 밖 존재: 폭이 사라졌다 돌아오며 페이지처럼 위상이 흔들린다.
                    transform.localScale = Vector3.Scale(_baseScale,
                        new Vector3(1f + wave * amount * 1.2f, 1f - wave * amount * .35f, 1f));
                    transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, lift * 2.2f);
                    target.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, .88f + .12f * Mathf.Abs(lift));
                    break;
                default:
                    // 보행: 발을 딛는 순간 세로로 눌리고 다음 보폭에서 복원된다.
                    float step = Mathf.Abs(wave) * (moving ? 1f : .16f);
                    transform.localScale = Vector3.Scale(_baseScale,
                        new Vector3(1f + step * amount * .45f, 1f - step * amount, 1f));
                    transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, wave * 1.15f);
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
            transform.localRotation = _baseRotation;
            if (target != null) target.color = _baseColor;
        }
    }
}
