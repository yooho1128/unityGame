using ShadowTheater.Battle;
using ShadowTheater.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowTheater.UI
{
    public class BattleUnitPanel : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private Image accent;
        [SerializeField] private Image hpFill;
        [SerializeField] private Text nameText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text hpText;
        [SerializeField] private Text statusText;

        private Vector3 _restPosition;
        private Vector3 _portraitPosition;
        private Vector3 _portraitScale;
        private Quaternion _portraitRotation;
        private ShadowRole _motionRole;
        private float _motionAmount = 0.025f;
        private float _motionPhase;
        public RectTransform MotionRoot => (RectTransform)transform;
        public Image Portrait => portrait;

        private void Awake()
        {
            _restPosition = MotionRoot.localPosition;
            if (portrait != null)
            {
                _portraitPosition = portrait.rectTransform.localPosition;
                _portraitScale = portrait.rectTransform.localScale;
                _portraitRotation = portrait.rectTransform.localRotation;
            }
            _motionPhase = (GetInstanceID() & 15) * 0.41f;
        }
        public void ResetMotion() => MotionRoot.localPosition = _restPosition;

        public void Bind(BattleUnit unit)
        {
            if (unit == null) return;
            _motionRole = unit.Data.role;
            _motionAmount = (int)unit.Data.growthTier >= (int)GrowthTier.RegionalBoss ? 0.045f : 0.025f;
            if (portrait != null)
            {
                portrait.sprite = unit.Instance.Silhouette;
                portrait.color = Color.black;
                portrait.preserveAspect = true;
            }
            if (accent != null) accent.color = unit.Instance.AccentColor;
            if (hpFill != null)
            {
                hpFill.fillAmount = unit.HpRatio;
                hpFill.color = unit.HpRatio > 0.5f ? new Color(0.35f, 0.92f, 0.65f)
                    : unit.HpRatio > 0.2f ? new Color(1f, 0.72f, 0.25f)
                    : new Color(1f, 0.28f, 0.36f);
            }
            if (nameText != null) nameText.text = unit.Name;
            if (levelText != null) levelText.text = $"Lv.{unit.Level}";
            if (hpText != null) hpText.text = $"{unit.Hp} / {unit.MaxHp}";
            if (statusText != null) statusText.text = unit.MajorStatus != null
                ? StatusLabel(unit.MajorStatus.type) : string.Empty;
        }

        private void LateUpdate()
        {
            if (portrait == null || !portrait.enabled) return;
            float speed = _motionRole == ShadowRole.SpeedUtility ? 5.5f
                : _motionRole == ShadowRole.MagicNuker || _motionRole == ShadowRole.Support ? 2.2f : 3.6f;
            float wave = Mathf.Sin((Time.unscaledTime + _motionPhase) * speed);
            float rise = (_motionRole == ShadowRole.MagicNuker || _motionRole == ShadowRole.Support)
                ? wave * 5f : Mathf.Abs(wave) * 2f;
            portrait.rectTransform.localPosition = _portraitPosition + Vector3.up * rise;
            portrait.rectTransform.localScale = Vector3.Scale(_portraitScale,
                new Vector3(1f - wave * _motionAmount * .25f, 1f + wave * _motionAmount, 1f));
            float tilt = _motionRole == ShadowRole.SpeedUtility ? wave * 2.5f : wave * .7f;
            portrait.rectTransform.localRotation = _portraitRotation * Quaternion.Euler(0f, 0f, tilt);
        }

        private static string StatusLabel(StatusEffectType status)
        {
            switch (status)
            {
                case StatusEffectType.Freeze: return "빙결";
                case StatusEffectType.Burn: return "화상";
                case StatusEffectType.Bleed: return "출혈";
                default: return status.ToString();
            }
        }
    }
}
