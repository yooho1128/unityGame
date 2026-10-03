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
        [SerializeField, Range(0.01f, 0.15f)] private float amount = 0.055f;
        [SerializeField, Range(1f, 12f)] private float speed = 5f;

        private Vector3 _baseScale;
        private Quaternion _baseRotation;
        private float _phase;

        public void Configure(ShadowData shadow)
        {
            target = GetComponent<SpriteRenderer>();
            if (shadow == null) return;
            role = shadow.role;
            growthTier = shadow.growthTier;
            speed = role == ShadowRole.SpeedUtility ? 7f
                : role == ShadowRole.MagicNuker || role == ShadowRole.Support ? 3.2f : 5.5f;
            amount = (int)growthTier >= (int)GrowthTier.RegionalBoss ? 0.075f : 0.05f;
        }

        private void Awake()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            _baseRotation = transform.localRotation;
            _phase = (GetInstanceID() & 31) * 0.37f;
        }

        private void LateUpdate()
        {
            if (target == null || !target.enabled) return;
            float wave = Mathf.Sin((Time.time + _phase) * speed);
            float lift = Mathf.Sin((Time.time + _phase) * speed * 0.5f);

            switch (role)
            {
                case ShadowRole.MagicNuker:
                case ShadowRole.Support:
                    // 공중 부유: 천천히 늘어나고 좌우로 떠돈다.
                    transform.localScale = Vector3.Scale(_baseScale,
                        new Vector3(1f - lift * amount * .3f, 1f + lift * amount, 1f));
                    transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, wave * 1.8f);
                    break;
                case ShadowRole.SpeedUtility:
                    // 날갯짓/활공: 빠른 좌우 기울기와 짧은 수축.
                    transform.localScale = Vector3.Scale(_baseScale,
                        new Vector3(1f + Mathf.Abs(wave) * amount, 1f - Mathf.Abs(wave) * amount * .45f, 1f));
                    transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, wave * 3.2f);
                    break;
                default:
                    // 보행: 발을 딛는 순간 세로로 눌리고 다음 보폭에서 복원된다.
                    float step = Mathf.Abs(wave);
                    transform.localScale = Vector3.Scale(_baseScale,
                        new Vector3(1f + step * amount * .45f, 1f - step * amount, 1f));
                    transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, wave * 1.15f);
                    break;
            }
        }

        private void OnDisable()
        {
            transform.localScale = _baseScale;
            transform.localRotation = _baseRotation;
        }
    }
}
